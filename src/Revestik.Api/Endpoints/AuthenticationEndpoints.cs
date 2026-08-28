using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Revestik.Api.Authorization;
using Revestik.Api.Models.Identity;
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

                    return Results.Challenge(
                        properties,
                        [GoogleDefaults.AuthenticationScheme]);
                })
            .WithName("LoginWithGoogle")
            .AllowAnonymous();

        group.MapGet(
                "/external-callback",
                HandleExternalCallbackAsync)
            .WithName("HandleExternalAuthentication")
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
            .RequireAuthorization();

        group.MapPost(
                "/logout",
                async (
                    LogoutRequest request,
                    SignInManager<ApplicationUser> signInManager) =>
                {
                    if (!request.Confirm)
                    {
                        return Results.BadRequest();
                    }

                    await signInManager.SignOutAsync();

                    return Results.NoContent();
                })
            .WithName("Logout")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> HandleExternalCallbackAsync(
        string? returnPath,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILoggerFactory loggerFactory)
    {
        var clientBaseUrl = GetClientBaseUrl(
            configuration,
            hostEnvironment);
        var safeReturnPath = NormalizeReturnPath(returnPath);
        var logger = loggerFactory.CreateLogger(
            "Revestik.Api.Authentication");

        var externalLogin =
            await signInManager.GetExternalLoginInfoAsync();

        if (externalLogin is null ||
            !string.Equals(
                externalLogin.LoginProvider,
                GoogleDefaults.AuthenticationScheme,
                StringComparison.Ordinal))
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

            return RedirectWithError(
                clientBaseUrl,
                "account_disabled");
        }

        var existingLogins =
            await userManager.GetLoginsAsync(user);

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

        return Results.Redirect(
            $"{clientBaseUrl}{safeReturnPath}");
    }

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
        if (string.IsNullOrWhiteSpace(returnPath) ||
            !returnPath.StartsWith('/') ||
            returnPath.StartsWith("//"))
        {
            return "/";
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
            $"{clientBaseUrl}/login",
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
