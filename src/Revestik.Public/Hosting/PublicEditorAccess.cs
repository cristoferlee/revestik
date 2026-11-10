using Revestik.Public;

namespace Revestik.Public.Hosting;

/// <summary>
/// The edit-mode preference is not authorization. Every SSR editor render still
/// requires the current Identity cookie and Administrator role, and every write
/// is independently protected by the admin API and CSRF validation.
/// </summary>
public static class PublicEditorAccess
{
    private const string PreferenceCookie = "Revestik.Public.EditMode";

    public static bool IsEditorRequested(HttpContext context)
    {
        // An explicit exit must override a preference that arrived on the same request.
        var command = context.Request.Query["edit"].ToString();
        return command != "0" && (command == "1" || context.Request.Cookies[PreferenceCookie] == "1");
    }

    public static bool CanEditPublicSite(HttpContext? context)
    {
        if (context is null) return false;

        var path = context.Request.Path.Value;
        if (string.IsNullOrEmpty(path) ||
            !SiteLinks.IsKnownPublicRoute("https://revestik.invalid" + path))
            return false;

        var command = context.Request.Query["edit"].ToString();
        if (command == "0")
        {
            // Set/delete cookies in SSR response headers, not in Blazor JS initialization.
            // This works on the very first request, on reload, and across page navigation.
            context.Response.OnStarting(() =>
            {
                context.Response.Cookies.Delete(PreferenceCookie, new CookieOptions
                {
                    Path = "/", Secure = true, SameSite = SameSiteMode.Lax
                });
                return Task.CompletedTask;
            });
            context.Response.Headers.CacheControl = "private, no-store";
            return false;
        }

        if (!IsEditorRequested(context) ||
            context.User.Identity?.IsAuthenticated != true ||
            !context.User.IsInRole("Administrator"))
            return false;

        if (command == "1")
        {
            // Browser-session preference. HttpOnly prevents client scripts from
            // mutating it; it never carries an identity or authorization claim.
            context.Response.OnStarting(() =>
            {
                context.Response.Cookies.Append(PreferenceCookie, "1", new CookieOptions
                {
                    Path = "/", Secure = true, HttpOnly = true,
                    SameSite = SameSiteMode.Lax, IsEssential = true
                });
                return Task.CompletedTask;
            });
        }

        // Admin-specific HTML (including interactive component markers) must
        // not enter shared caches or search-engine indexes.
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        return true;
    }
}
