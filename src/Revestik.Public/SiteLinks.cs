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

    public static string CartPath(string lang) => (lang is "en" or "de" ? lang : "es") switch
    {
        "en" => "/en/cart",
        "de" => "/de/warenkorb",
        _ => "/es/carrito"
    };

    public static bool IsCartRoute(string uri)
    {
        var parts = new Uri(uri).AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 &&
            string.Equals("/" + parts[0] + "/" + parts[1], CartPath(parts[0]), StringComparison.Ordinal);
    }

    // Public projects have language-neutral slugs. Validate their shape, not a hard-coded example list.
    public static bool IsProjectSlug(string? slug) =>
        slug is { Length: >= 1 and <= 160 } && slug[0] != '-' && slug[^1] != '-' &&
        slug.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-');

    // Preserve project detail context when changing languages.
    public static string LanguagePath(string language, string currentUri)
    {
        var sourceUri = new Uri(currentUri);
        var current = sourceUri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (IsCartRoute(currentUri)) return CartPath(language);
        var page = PageFromUri(currentUri);
        var target = Path(language, page);
        if (page == 1 && current.Length == 3 && IsProjectSlug(current[2]))
            target += "/" + current[2];
        else if (page == 2 && current.Length == 3 && current[2] is not ("categoria" or "category" or "kategorie"))
            target += "/" + current[2];
        else if (page == 2 && current.Length == 4 && current[2] is "categoria" or "category" or "kategorie")
            target += "/" + (language == "en" ? "category" : language == "de" ? "kategorie" : "categoria") + "/" + current[3];

        // Preserve only supported public listing filters, not editor/session flags.
        if ((page == 2 || page == 1 && current.Length == 2) && sourceUri.Query.Length > 1)
        {
            var filters = sourceUri.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(part =>
                {
                    var key = Uri.UnescapeDataString(part.Split('=', 2)[0]);
                    return page == 1 ? key == "page" : key is "page" or "search" or "categoryId" or "subcategoryId";
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
        // Cart is intentionally NOT an editable/SEO-canonical editorial page.
        // Razor routing handles it independently; keep the editor allowlist unchanged.
        if (IsCartRoute(uri)) return false;
        if (parts.Length == 1) return true;
        var page = PageFromUri(uri);
        if (page == 0 || Path(current, page).Trim('/').Split('/').Last() != parts[1]) return false;
        return parts.Length == 2 || (page == 1 && parts.Length == 3 && IsProjectSlug(parts[2])) ||
            (page == 2 && parts.Length == 3 && parts[2] is not ("categoria" or "category" or "kategorie")) ||
            (page == 2 && parts.Length == 4 && parts[2] is "categoria" or "category" or "kategorie");
    }

    // OAuth destinations are public navigation routes, not arbitrary URLs.
    public static string NormalizeAuthenticationReturnPath(
        string? candidate, string language)
    {
        language = language is "en" or "de" ? language : "es";
        var fallback = Path(language, 0);

        if (string.IsNullOrWhiteSpace(candidate) ||
            candidate.Length > 2048 ||
            !candidate.StartsWith("/", StringComparison.Ordinal) ||
            candidate.StartsWith("//", StringComparison.Ordinal) ||
            candidate.Contains('\\') ||
            candidate.Contains('#') ||
            candidate.Any(char.IsControl))
            return fallback;

        var rawPath = candidate.Split('?', 2)[0];

        // Reject encoded separators and ambiguous path normalization.
        if (rawPath.Contains('%') ||
            rawPath.Contains("//", StringComparison.Ordinal) ||
            rawPath.Any(char.IsWhiteSpace))
            return fallback;

        var parts = rawPath.Trim('/').Split('/');

        if (parts.Length == 0 ||
            parts[0] != language ||
            parts.Any(part => part is "." or ".."))
            return fallback;

        if (parts.Length == 1)
            return fallback;

        var section = "/" + parts[0] + "/" + parts[1];

        if (section == CartPath(language))
            return parts.Length == 2 ? section : fallback;

        var page = Enumerable.Range(1, 5)
            .FirstOrDefault(index => Path(language, index) == section);

        if (page == 0)
            return fallback;

        var normalized = section;

        if (parts.Length > 2)
        {
            var categorySegment = language switch
            {
                "en" => "category",
                "de" => "kategorie",
                _ => "categoria"
            };

            if (parts.Length == 3 &&
                page is 1 or 2 &&
                IsProjectSlug(parts[2]) &&
                parts[2] is not ("categoria" or "category" or "kategorie"))
            {
                normalized += "/" + parts[2];
            }
            else if (parts.Length == 4 &&
                     page == 2 &&
                     parts[2] == categorySegment &&
                     IsProjectSlug(parts[3]))
            {
                normalized += "/" + categorySegment + "/" + parts[3];
            }
            else
            {
                return fallback;
            }
        }

        // Only preserve supported listing filters. Strip editor, auth,
        // session and unknown query parameters.
        if (parts.Length != 2 || page is not (1 or 2))
            return normalized;

        var queryIndex = candidate.IndexOf('?');

        if (queryIndex < 0)
            return normalized;

        var parsed = Microsoft.AspNetCore.WebUtilities.QueryHelpers
            .ParseQuery(candidate[queryIndex..]);

        var filters = new Dictionary<string, string?>();

        foreach (var pair in parsed)
        {
            if (pair.Value.Count != 1)
                continue;

            var value = pair.Value.ToString();

            if (pair.Key == "page" ||
                page == 2 && pair.Key is "categoryId" or "subcategoryId")
            {
                if (int.TryParse(value, out var number) && number > 0)
                    filters[pair.Key] = number.ToString(
                        System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (page == 2 &&
                     pair.Key == "search" &&
                     value.Length is > 0 and <= 200 &&
                     !value.Any(char.IsControl))
            {
                filters["search"] = value;
            }
        }

        return filters.Count == 0
            ? normalized
            : Microsoft.AspNetCore.WebUtilities.QueryHelpers
                .AddQueryString(normalized, filters);
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
        if (IsCartRoute(uri)) return -1;
        if (path.Length < 2) return 0;
        var lang = path[0];
        var slugs = lang == "en" ? SlugsEn : lang == "de" ? SlugsDe : SlugsEs;
        var idx = Array.IndexOf(slugs, path[1]);
        return idx < 0 ? 0 : idx;
    }
}
