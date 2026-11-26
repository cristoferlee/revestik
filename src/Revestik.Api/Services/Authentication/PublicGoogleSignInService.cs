using System.Data;
using Revestik.Shared.Authentication;
using Revestik.Public;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Endpoints;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Audit;

namespace Revestik.Api.Services.Authentication;

public enum PublicGoogleAccountStatus
{
    Existing,
    Created,
    Conflict,
    Disabled,
    InvalidIdentity,
    RegistrationDisabled,
    ProvisioningFailed
}

public sealed record PublicGoogleAccountResult(
    PublicGoogleAccountStatus Status,
    ApplicationUser? User = null);

public static class GoogleLoginFlow
{
    private const string IntentKey = "revestik:google-login-intent";
    private const string PublicLanguageKey = "revestik:public-language";
    private const string PublicReturnPathKey = "revestik:public-return-path";

    public const string Internal = "internal";
    public const string Public = "public";

    public static void SetIntent(
        Microsoft.AspNetCore.Authentication.AuthenticationProperties properties,
        string intent) => properties.Items[IntentKey] = intent;

    public static bool HasIntent(ExternalLoginInfo? login, string intent)
    {
        if (login?.AuthenticationProperties?.Items is not { } items)
            return false;
        return items.TryGetValue(IntentKey, out var stored) &&
               string.Equals(stored, intent, StringComparison.Ordinal);
    }

    // Locale travels with the protected OAuth state and never becomes an arbitrary return URL.
    public static string NormalizePublicLanguage(string? language) =>
        language is "en" or "de" ? language : "es";

    public static void SetPublicLanguage(
        Microsoft.AspNetCore.Authentication.AuthenticationProperties properties,
        string? language) =>
        properties.Items[PublicLanguageKey] = NormalizePublicLanguage(language);

    public static string GetPublicLanguage(ExternalLoginInfo? login)
    {
        if (login?.AuthenticationProperties?.Items is { } items &&
            items.TryGetValue(PublicLanguageKey, out var language))
            return NormalizePublicLanguage(language);
        return "es";
    }

    public static void SetPublicReturnPath(
        Microsoft.AspNetCore.Authentication.AuthenticationProperties properties,
        string? returnPath,
        string? language)
    {
        properties.Items[PublicReturnPathKey] =
            SiteLinks.NormalizeAuthenticationReturnPath(
                returnPath, NormalizePublicLanguage(language));
    }

    public static string GetPublicReturnPath(ExternalLoginInfo? login)
    {
        var language = GetPublicLanguage(login);
        string? returnPath = null;

        if (login?.AuthenticationProperties?.Items is { } items)
            items.TryGetValue(PublicReturnPathKey, out returnPath);

        return SiteLinks.NormalizeAuthenticationReturnPath(
            returnPath, language);
    }

    public static int PublicSessionVersion(IConfiguration configuration)
    {
        var configured = configuration[
            PublicAuthenticationRules.SessionVersionKey];

        if (configured is null)
            return 1;

        return int.TryParse(
                   configured,
                   System.Globalization.NumberStyles.None,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out var version) &&
               version > 0
            ? version
            : 0;
    }

    public static bool HasCurrentPublicSessionVersion(
        Microsoft.AspNetCore.Authentication.AuthenticationProperties properties,
        int currentVersion)
    {
        if (currentVersion <= 0)
            return false;

        return properties.Items.TryGetValue(
                   PublicAuthenticationRules.SessionVersionProperty,
                   out var storedVersion) &&
               string.Equals(
                   storedVersion,
                   currentVersion.ToString(
                       System.Globalization.CultureInfo.InvariantCulture),
                   StringComparison.Ordinal);
    }

    // Public sessions expire after four hours without sliding renewal.
    // Administration keeps its own existing Identity cookie configuration.
    public static Microsoft.AspNetCore.Authentication.AuthenticationProperties
        PublicSessionProperties(
            DateTimeOffset issuedAtUtc,
            int sessionVersion)
    {
        if (sessionVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(sessionVersion));

        var properties =
            new Microsoft.AspNetCore.Authentication.AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = issuedAtUtc.AddHours(4),
                AllowRefresh = false
            };

        properties.Items[PublicAuthenticationRules.SessionVersionProperty] =
            sessionVersion.ToString(
                System.Globalization.CultureInfo.InvariantCulture);

        return properties;
    }

    public static bool HasEmployeeRole(ICollection<string> roles) =>
        roles.Count == 1 &&
        RoleNames.All.Contains(roles.First(), StringComparer.Ordinal);

    public static bool HasPublicRole(ICollection<string> roles) =>
        roles.Count == 1 &&
        string.Equals(roles.First(), RoleNames.PublicCustomer, StringComparison.Ordinal);

    public static bool PublicLoginEnabled(IConfiguration configuration) =>
        configuration.GetValue<bool>(
            PublicAuthenticationRules.LoginEnabledKey);

    public static bool PublicRegistrationEnabled(IConfiguration configuration) =>
        PublicAuthenticationRules.CanRegister(
            PublicLoginEnabled(configuration),
            configuration.GetValue<bool>(
                PublicAuthenticationRules.RegistrationEnabledKey));
}

public sealed class PublicGoogleSignInService(
    RevestikDbContext db,
    UserManager<ApplicationUser> userManager,
    ILogger<PublicGoogleSignInService> logger)
{
    public async Task<PublicGoogleAccountResult> ResolveAsync(
        ExternalLoginInfo login,
        CancellationToken ct,
        bool allowRegistration)
    {
        var email = login.Principal.FindFirstValue(ClaimTypes.Email)?.Trim();

        if (!string.Equals(login.LoginProvider, GoogleDefaults.AuthenticationScheme, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(login.ProviderKey) ||
            string.IsNullOrWhiteSpace(email) ||
            email.Length > 256 ||
            !GoogleEmailVerification.IsVerified(login.Principal))
        {
            return new(PublicGoogleAccountStatus.InvalidIdentity);
        }

        try
        {
            // A single transaction binds the email, provider identity, public role and audit.
            // SQL Server SERIALIZABLE protects parallel first registrations; Identity's
            // normalized username and (provider, subject) unique indexes reinforce it.
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, ct);

            var user = await userManager.FindByLoginAsync(
                login.LoginProvider, login.ProviderKey);

            if (user is not null)
            {
                var roles = await userManager.GetRolesAsync(user);

                if (!GoogleLoginFlow.HasPublicRole(roles) ||
                    !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("Public Google login rejected due to a conflicting existing identity.");
                    return new(PublicGoogleAccountStatus.Conflict);
                }

                if (!user.IsActive || !user.EmailConfirmed)
                {
                    SecurityAudit.Record(db, "PublicLoginRejectedDisabled", "User", user.Id, user.Id);
                    await db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return new(PublicGoogleAccountStatus.Disabled);
                }

                return new(PublicGoogleAccountStatus.Existing, user);
            }

            // Never link a public Google login to an existing user solely by email.
            // This explicitly excludes pre-authorized employees and previous public
            // accounts whose Google provider subject is different.
            if (await userManager.FindByEmailAsync(email) is not null)
            {
                logger.LogWarning("Public Google registration rejected: email already belongs to an account.");
                return new(PublicGoogleAccountStatus.Conflict);
            }

            if (!allowRegistration)
                return new(PublicGoogleAccountStatus.RegistrationDisabled);

            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = GetDisplayName(login.Principal, email),
                IsActive = true,
                LockoutEnabled = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            if (!await ApplyAsync(userManager.CreateAsync(user)))
                return new(PublicGoogleAccountStatus.ProvisioningFailed);

            if (!await ApplyAsync(userManager.AddToRoleAsync(user, RoleNames.PublicCustomer)))
                return new(PublicGoogleAccountStatus.ProvisioningFailed);

            if (!await ApplyAsync(userManager.AddLoginAsync(user, login)))
                return new(PublicGoogleAccountStatus.ProvisioningFailed);

            SecurityAudit.Record(db, "PublicGoogleAccountCreated", "User", user.Id, user.Id);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new(PublicGoogleAccountStatus.Created, user);
        }
        catch (Exception exception) when (IsSqlConflict(exception))
        {
            // EF Core may wrap SQL deadlock 1205 in DbUpdateException and
            // InvalidOperationException. Fail closed instead of exposing a 500.
            logger.LogWarning("Concurrent public account registration encountered a SQL conflict or deadlock.");
            return new(PublicGoogleAccountStatus.Conflict);
        }
    }

    private async Task<bool> ApplyAsync(Task<IdentityResult> operation)
    {
        var result = await operation;
        if (result.Succeeded) return true;

        logger.LogWarning("Public account provisioning failed: {ErrorCodes}",
            string.Join(",", result.Errors.Select(error => error.Code)));
        return false;
    }

    private static bool IsSqlConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 2601 or 2627 or 1205 })
                return true;
        }

        return false;
    }

    private static string GetDisplayName(ClaimsPrincipal principal, string email)
    {
        var name = principal.FindFirstValue(ClaimTypes.Name)?.Trim();
        var displayName = string.IsNullOrWhiteSpace(name) ? email : name;
        return displayName.Length > 150 ? displayName[..150] : displayName;
    }
}
