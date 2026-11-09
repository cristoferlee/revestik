namespace Revestik.Public;

public static class SiteLinks
{
    private static readonly string[] SlugsEs = ["", "proyectos", "acabados", "soluciones", "nosotros", "contacto"];
    private static readonly string[] SlugsEn = ["", "projects", "finishes", "solutions", "about", "contact"];
    private static readonly string[] SlugsDe = ["", "projekte", "oberflaechen", "loesungen", "ueber-uns", "kontakt"];

    public static string Path(string lang, int page)
    {
        lang = lang is "en" or "de" ? lang : "es";
        var slug = (lang == "en" ? SlugsEn : lang == "de" ? SlugsDe : SlugsEs)[page];
        return $"/{lang}" + (slug.Length == 0 ? "" : $"/{slug}");
    }

    // Keep project detail context when switching languages.
    // A future published-content service must resolve localized slugs independently.
    public static string LanguagePath(string language, string currentUri)
    {
        var sourceUri = new Uri(currentUri);
        var current = sourceUri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var page = PageFromUri(currentUri);
        var target = Path(language, page);
        if (page == 1 && current.Length == 3 && PublicProjectCatalog.IsKnownSlug(current[2]))
            target += "/" + current[2];
        else if (page == 2 && current.Length == 3 && current[2] is not ("categoria" or "category" or "kategorie"))
            target += "/" + current[2];
        else if (page == 2 && current.Length == 4 && current[2] is "categoria" or "category" or "kategorie")
            target += "/" + (language == "en" ? "category" : language == "de" ? "kategorie" : "categoria") + "/" + current[3];

        // Preserve only supported catalog filters when switching languages. Never copy editor/session flags.
        if (page == 2 && sourceUri.Query.Length > 1)
        {
            var filters = sourceUri.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(part =>
                {
                    var key = Uri.UnescapeDataString(part.Split('=', 2)[0]);
                    return key is "page" or "search" or "categoryId" or "subcategoryId";
                });
            var query = string.Join("&", filters);
            if (query.Length > 0) target += "?" + query;
        }
        return target;
    }

    public static bool IsKnownPublicRoute(string uri)
    {
        var parts = new Uri(uri).AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts[0] is not ("es" or "en" or "de")) return false;
        var current = parts[0];
        if (parts.Length == 1) return true;
        var page = PageFromUri(uri);
        if (page == 0 || Path(current, page).Trim('/').Split('/').Last() != parts[1]) return false;
        return parts.Length == 2 || (page == 1 && parts.Length == 3 &&
            PublicProjectCatalog.IsKnownSlug(parts[2])) ||
            (page == 2 && parts.Length == 3 && parts[2] is not ("categoria" or "category" or "kategorie")) ||
            (page == 2 && parts.Length == 4 && parts[2] is "categoria" or "category" or "kategorie");
    }

    public static string? ValidatedPublicBaseUrl(string? configured)
    {
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment) || url.AbsolutePath != "/") return null;
        return url.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    public static int PageFromUri(string uri)
    {
        var path = new Uri(uri).AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (path.Length < 2) return 0;
        var lang = path[0];
        var slugs = lang == "en" ? SlugsEn : lang == "de" ? SlugsDe : SlugsEs;
        var idx = Array.IndexOf(slugs, path[1]);
        return idx < 0 ? 0 : idx;
    }
}
