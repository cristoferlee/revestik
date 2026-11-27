using System.Security.Claims;

namespace Revestik.Shared.Authentication;

public static class PublicAuthenticationRules
{
    public const string LoginEnabledKey =
        "Authentication:PublicGoogleLoginEnabled";

    public const string RegistrationEnabledKey =
        "Authentication:PublicGoogleRegistrationEnabled";

    public const string CustomerRole = "PublicCustomer";

    public const string RegistrationPreviewPath = "/es/crear-cuenta";
    public const string RegistrationPostPath = "/api/auth/public/register/google";
    public const string RegistrationNoticeVersion = "es-preview-1";
    public const string RegistrationNoticeLanguage = "es";

    public const string SessionVersionKey =
        "Authentication:PublicSessionVersion";

    public const string SessionVersionProperty =
        "revestik:public-session-version";

    public static bool CanRegister(
        bool loginEnabled,
        bool registrationEnabled) =>
        loginEnabled && registrationEnabled;

    public static bool IsEmployee(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
            return false;

        var roles = principal.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return roles.Length == 1 &&
            roles[0] is "Administrator" or "Accountant" or
                "Sales" or "Warehouse";
    }

    public static bool IsPublicCustomer(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
            return false;

        var roles = principal.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return roles.Length == 1 &&
            string.Equals(
                roles[0],
                CustomerRole,
                StringComparison.Ordinal);
    }
}
