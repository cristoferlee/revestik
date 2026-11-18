using Revestik.Api.Extensions;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.SqlClient;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Services.Audit;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Authentication;
using Revestik.Shared.Authentication;

namespace Revestik.Api.Endpoints;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapGet(
                "/providers",
                async (
                    IAuthenticationSchemeProvider schemeProvider) =>
                {
                    var googleScheme =
                        await schemeProvider.GetSchemeAsync(
                            GoogleDefaults.AuthenticationScheme);

                    return Results.Ok(
                        new AuthenticationProviderResponse(
                            GoogleDefaults.AuthenticationScheme,
                            "Google",
                            googleScheme is not null));
                })
            .WithName("GetAuthenticationProviders")
            .AllowAnonymous();

        group.MapGet(
                "/csrf",
                (
                    HttpContext context,
                    IAntiforgery antiforgery) =>
                {
                    var tokens =
                        antiforgery.GetAndStoreTokens(context);

                    if (tokens.RequestToken is null)
                    {
                        return Results.Problem(
                            title: "CSRF token generation failed.",
                            statusCode:
                                StatusCodes.Status500InternalServerError);
                    }

                    context.Response.Headers.CacheControl =
                        "no-store, no-cache";

                    return Results.Ok(
                        new CsrfTokenResponse(
                            tokens.RequestToken));
                })
            .WithName("GetCsrfToken")
            .RequireAuthorization(PolicyNames.AuthenticatedUser);

        group.MapGet(
            "/login/google",
                async (
                    string? returnPath,
                    SignInManager<ApplicationUser> signInManager,
                    IAuthenticationSchemeProvider schemeProvider) =>
                {
                    var googleScheme =
                        await schemeProvider.GetSchemeAsync(
                            GoogleDefaults.AuthenticationScheme);

                    if (googleScheme is null)
                    {
                        return Results.Problem(
                            title:
                                "Google authentication is unavailable.",
                            statusCode:
                                StatusCodes.Status503ServiceUnavailable);
                    }

                    var safeReturnPath =
                        NormalizeReturnPath(returnPath);

                    var callbackUrl = QueryHelpers.AddQueryString(
                        "/api/auth/external-callback",
                        "returnPath",
                        safeReturnPath);

                    var properties =
                        signInManager
                            .ConfigureExternalAuthenticationProperties(
                                GoogleDefaults.AuthenticationScheme,
                                callbackUrl);
                    GoogleLoginFlow.SetIntent(properties, GoogleLoginFlow.Internal);

                    return Results.Challenge(
                        properties,
                        [GoogleDefaults.AuthenticationScheme]);
                })
            .RequireRateLimiting(RevestikRateLimiting.Login)
            .WithName("LoginWithGoogle")
            .AllowAnonymous();

        group.MapGet(
                "/external-callback",
                HandleExternalCallbackAsync)
            .WithName("HandleExternalAuthentication")
            .AllowAnonymous();

        // Public and employee OAuth use the same Google provider but separate
        // application callbacks. The login intent travels in protected OAuth state.
        group.MapGet(
                "/public/login/google",
                async (
                    string? language,
                    SignInManager<ApplicationUser> signInManager,
                    IAuthenticationSchemeProvider schemeProvider,
                    IConfiguration configuration,
                    IHostEnvironment environment) =>
                {
                    if (!GoogleLoginFlow.PublicRegistrationEnabled(configuration, environment))
                        return Results.NotFound();

                    if (await schemeProvider.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) is null)
                        return Results.Problem(
                            title: "Google authentication is unavailable.",
                            statusCode: StatusCodes.Status503ServiceUnavailable);

                    var properties = signInManager.ConfigureExternalAuthenticationProperties(
                        GoogleDefaults.AuthenticationScheme,
                        "/api/auth/public/external-callback");
                    GoogleLoginFlow.SetIntent(properties, GoogleLoginFlow.Public);
                    GoogleLoginFlow.SetPublicLanguage(properties, language);
                    return Results.Challenge(properties, [GoogleDefaults.AuthenticationScheme]);
                })
            .RequireRateLimiting(RevestikRateLimiting.Login)
            .WithName("PublicGoogleLogin")
            .AllowAnonymous();

        group.MapGet(
                "/public/external-callback",
                HandlePublicExternalCallbackAsync)
            .RequireRateLimiting(RevestikRateLimiting.Login)
            .WithName("PublicGoogleExternalCallback")
            .AllowAnonymous();

        group.MapGet(
                "/me",
                async (
                    ClaimsPrincipal principal,
                    UserManager<ApplicationUser> userManager) =>
                {
                    var user =
                        await userManager.GetUserAsync(principal);

                    if (user is null || !user.IsActive)
                    {
                        return Results.Unauthorized();
                    }

                    var roles =
                        await userManager.GetRolesAsync(user);

                    return Results.Ok(
                        new CurrentUserResponse(
                            user.Id,
                            user.DisplayName,
                            user.Email ?? string.Empty,
                            roles.ToArray()));
                })
            .WithName("GetCurrentUser")
            .RequireAuthorization(PolicyNames.AuthenticatedUser);

        group.MapPost(
                "/logout",
                async (
                    LogoutRequest request,
                    SignInManager<ApplicationUser> signInManager,
                    RevestikDbContext db, ClaimsPrincipal principal, CancellationToken ct) =>
                {
                    if (!request.Confirm)
                    {
                        return Results.BadRequest();
                    }

                    await signInManager.SignOutAsync();
                    SecurityAudit.Record(db, "Logout", "Session", null,
                        principal.FindFirstValue(ClaimTypes.NameIdentifier));
                    await db.SaveChangesAsync(ct);

                    return Results.NoContent();
                })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("Logout")
            .RequireAuthorization(PolicyNames.AuthenticatedUser);

        return endpoints;
    }

    private static async Task<IResult> HandleExternalCallbackAsync(
        string? returnPath,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILoggerFactory loggerFactory,
        RevestikDbContext db, CancellationToken ct)
    {
        var clientBaseUrl = GetClientBaseUrl(
            configuration,
            hostEnvironment);
        var safeReturnPath = NormalizeReturnPath(returnPath);
        var logger = loggerFactory.CreateLogger(
            "Revestik.Api.Authentication");

        try
        {
            var externalLogin =
                await signInManager.GetExternalLoginInfoAsync();

            if (externalLogin is null ||
                !string.Equals(
                    externalLogin.LoginProvider,
                    GoogleDefaults.AuthenticationScheme,
                    StringComparison.Ordinal) ||
                !GoogleLoginFlow.HasIntent(externalLogin, GoogleLoginFlow.Internal))
            {
                logger.LogWarning(
                    "Google authentication callback did not contain valid external login information.");

                return RedirectWithError(
                    clientBaseUrl,
                    "external_login_failed");
            }

            var email = externalLogin.Principal.FindFirstValue(
                ClaimTypes.Email);

            if (string.IsNullOrWhiteSpace(email))
            {
                logger.LogWarning(
                    "Google authentication did not return an email address.");

                return RedirectWithError(
                    clientBaseUrl,
                    "email_not_available");
            }

            email = email.Trim();

            if (!GoogleEmailVerification.IsVerified(externalLogin.Principal))
            {
                logger.LogWarning("Google sign-in rejected: email not verified.");
                return RedirectWithError(clientBaseUrl, "email_not_verified");
            }

            var user = await userManager.FindByLoginAsync(
                externalLogin.LoginProvider,
                externalLogin.ProviderKey);

            if (user is null)
            {
                user = await userManager.FindByEmailAsync(email);
            }

            if (user is null)
            {
                var bootstrapEmail = configuration[
                    "Authentication:BootstrapAdministratorEmail"];

                if (!string.Equals(
                        email,
                        bootstrapEmail?.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning(
                        "Google login was denied because the account is not authorized.");

                    return RedirectWithError(
                        clientBaseUrl,
                        "account_not_authorized");
                }

                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    DisplayName = GetDisplayName(
                        externalLogin.Principal,
                        email),
                    IsActive = true,
                    LockoutEnabled = true,
                    CreatedAtUtc = DateTime.UtcNow
                };

                var createResult =
                    await userManager.CreateAsync(user);

                if (!createResult.Succeeded)
                {
                    LogIdentityErrors(
                        logger,
                        "create bootstrap administrator",
                        createResult.Errors);

                    return RedirectWithError(
                        clientBaseUrl,
                        "account_creation_failed");
                }

                var roleResult = await userManager.AddToRoleAsync(
                    user,
                    RoleNames.Administrator);

                if (!roleResult.Succeeded)
                {
                    await userManager.DeleteAsync(user);

                    LogIdentityErrors(
                        logger,
                        "assign administrator role",
                        roleResult.Errors);

                    return RedirectWithError(
                        clientBaseUrl,
                        "role_assignment_failed");
                }
            }

            if (!user.IsActive)
            {
                logger.LogWarning(
                    "A disabled user attempted to sign in.");
                SecurityAudit.Record(db, "LoginRejectedDisabled", "User", user.Id, user.Id);
                await db.SaveChangesAsync(ct);

                return RedirectWithError(
                    clientBaseUrl,
                    "account_disabled");
            }

            var internalRoles = await userManager.GetRolesAsync(user);
            if (!GoogleLoginFlow.HasEmployeeRole(internalRoles))
            {
                logger.LogWarning("Administrative Google login rejected: account has no valid employee role.");
                return RedirectWithError(clientBaseUrl, "account_not_authorized");
            }

            var existingLogins =
                await userManager.GetLoginsAsync(user);

            // A pre-authorized employee may link Google on their first login.
            // An employee already linked to a DIFFERENT Google subject must not
            // silently attach another subject merely because its email matches.
            if (existingLogins.Any(login =>
                string.Equals(login.LoginProvider, GoogleDefaults.AuthenticationScheme, StringComparison.Ordinal) &&
                !string.Equals(login.ProviderKey, externalLogin.ProviderKey, StringComparison.Ordinal)))
            {
                logger.LogWarning("Administrative Google login rejected: provider subject mismatch.");
                return RedirectWithError(clientBaseUrl, "account_not_authorized");
            }

            var hasGoogleLogin = existingLogins.Any(login =>
                string.Equals(
                    login.LoginProvider,
                    externalLogin.LoginProvider,
                    StringComparison.Ordinal) &&
                string.Equals(
                    login.ProviderKey,
                    externalLogin.ProviderKey,
                    StringComparison.Ordinal));

            if (!hasGoogleLogin)
            {
                var loginResult = await userManager.AddLoginAsync(
                    user,
                    externalLogin);

                if (!loginResult.Succeeded)
                {
                    LogIdentityErrors(
                        logger,
                        "link Google login",
                        loginResult.Errors);

                    return RedirectWithError(
                        clientBaseUrl,
                        "login_link_failed");
                }
            }

            user.LastLoginAtUtc = DateTime.UtcNow;

            var updateResult = await userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                LogIdentityErrors(
                    logger,
                    "update last login time",
                    updateResult.Errors);

                return RedirectWithError(
                    clientBaseUrl,
                    "account_update_failed");
            }

            await signInManager.SignInAsync(
                user,
                isPersistent: false,
                externalLogin.LoginProvider);

            SecurityAudit.Record(db, "LoginSucceeded", "User", user.Id, user.Id);
            await db.SaveChangesAsync(ct);

            return Results.Redirect(
                $"{clientBaseUrl}{safeReturnPath}");
        }
        catch (SqlException ex) when (ex.Number == -2 && !ct.IsCancellationRequested)
        {
            logger.LogError(ex,
                "SQL connection timed out during Google authentication callback.");

            var loginUrl = QueryHelpers.AddQueryString(
                $"{clientBaseUrl}/administracion/login",
                new Dictionary<string, string?>
                {
                    ["error"] = "database_temporarily_unavailable",
                    ["returnPath"] = safeReturnPath
                });

            return Results.Redirect(loginUrl);
        }
    }

    private static async Task<IResult> HandlePublicExternalCallbackAsync(
        SignInManager<ApplicationUser> signInManager,
        IConfiguration configuration,
        IHostEnvironment environment,
        PublicGoogleSignInService publicGoogleSignIn,
        RevestikDbContext db,
        UserManager<ApplicationUser> userManager,
        HttpContext context,
        CancellationToken ct)
    {
        if (!GoogleLoginFlow.PublicRegistrationEnabled(configuration, environment))
            return Results.NotFound();

        var externalLogin = await signInManager.GetExternalLoginInfoAsync();
        var language = GoogleLoginFlow.GetPublicLanguage(externalLogin);
        if (!GoogleLoginFlow.HasIntent(externalLogin, GoogleLoginFlow.Public))
            return PublicLoginError("external_login_failed", language);

        var result = await publicGoogleSignIn.ResolveAsync(externalLogin!, ct);
        if (result.User is null)
        {
            var error = result.Status switch
            {
                PublicGoogleAccountStatus.Disabled => "account_disabled",
                PublicGoogleAccountStatus.InvalidIdentity => "external_login_failed",
                PublicGoogleAccountStatus.Conflict => "account_conflict",
                _ => "registration_failed"
            };
            return PublicLoginError(error, language);
        }

        result.User.LastLoginAtUtc = DateTime.UtcNow;
        if (!(await userManager.UpdateAsync(result.User)).Succeeded)
            return PublicLoginError("registration_failed", language);

        // Persist the successful-login audit before issuing the browser session.
        // If audit persistence fails, the app authentication cookie is not issued.
        SecurityAudit.Record(db, "PublicLoginSucceeded", "User", result.User.Id, result.User.Id);
        await db.SaveChangesAsync(ct);
        await context.SignOutAsync(IdentityConstants.ExternalScheme);
        await signInManager.SignInAsync(
            result.User,
            GoogleLoginFlow.PublicSessionProperties(DateTimeOffset.UtcNow),
            GoogleDefaults.AuthenticationScheme);

        return Results.Redirect($"/{language}");
    }

    private static IResult PublicLoginError(string errorCode, string language) =>
        Results.Redirect(QueryHelpers.AddQueryString($"/{language}", "authError", errorCode));

    private static string GetClientBaseUrl(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        if (!hostEnvironment.IsDevelopment())
        {
            return string.Empty;
        }

        var clientBaseUrl =
            configuration["Authentication:ClientBaseUrl"]
            ?? throw new InvalidOperationException(
                "Authentication client base URL was not configured.");

        return clientBaseUrl.TrimEnd('/');
    }

    private static string NormalizeReturnPath(string? returnPath)
    {
        // Only allow redirects inside the business application after OAuth.
        if (string.IsNullOrWhiteSpace(returnPath) ||
            returnPath.StartsWith("//", StringComparison.Ordinal) ||
            returnPath.Contains('\\') ||
            !(returnPath.Equals("/administracion", StringComparison.OrdinalIgnoreCase) ||
              returnPath.StartsWith("/administracion/", StringComparison.OrdinalIgnoreCase)))
        {
            return "/administracion/";
        }

        return returnPath;
    }

    private static string GetDisplayName(
        ClaimsPrincipal principal,
        string fallbackEmail)
    {
        var displayName =
            principal.FindFirstValue(ClaimTypes.Name);

        return string.IsNullOrWhiteSpace(displayName)
            ? fallbackEmail
            : displayName.Trim();
    }

    private static IResult RedirectWithError(
        string clientBaseUrl,
        string errorCode)
    {
        var loginUrl = QueryHelpers.AddQueryString(
            $"{clientBaseUrl}/administracion/login",
            "error",
            errorCode);

        return Results.Redirect(loginUrl);
    }

    private static void LogIdentityErrors(
        ILogger logger,
        string operation,
        IEnumerable<IdentityError> identityErrors)
    {
        logger.LogError(
            "Identity operation {Operation} failed with codes: {ErrorCodes}",
            operation,
            string.Join(
                ",",
                identityErrors.Select(error => error.Code)));
    }
}

// Google maps this provider-specific claim in AuthenticationExtensions.
// An absent or non-true value must fail closed before email-based account linking.
public static class GoogleEmailVerification
{
    public static bool IsVerified(ClaimsPrincipal principal) =>
        string.Equals(
            principal.FindFirstValue("urn:google:email_verified"),
            "true", StringComparison.OrdinalIgnoreCase);
}
