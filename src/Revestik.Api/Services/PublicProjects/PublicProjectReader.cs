using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models.PublicProjects;
using Revestik.Api.Services.PublicCatalog;

namespace Revestik.Api.Services.PublicProjects;

// Public-only read model. No operational Project, Customer or inventory Product data.
public static class PublicProjectReader
{
    public const int PageSize = 12;

    public sealed record Preview(
        int Id, string Slug, string TypeCode, string? Location,
        string Name, string Description, int CoverImageId, string CoverAlt,
        IReadOnlyList<string> SpaceCodes);

    public sealed record Page(int PageNumber, int PageSize, int Total, IReadOnlyList<Preview> Items);

    public sealed record Hotspot(
        int Id, decimal AnchorX, decimal AnchorY, decimal LabelX, decimal LabelY,
        int DisplayOrder, string Label, string ProductName, bool ProductAvailable,
        string? ProductSlug);

    public sealed record Photo(
        int Id, string Role, int DisplayOrder, int Width, int Height, string Alt,
        IReadOnlyList<Hotspot> Tags);

    public sealed record Detail(
        int Id, string Slug, string TypeCode, string? Location, string Name,
        string Description, string SeoTitle, string SeoDescription,
        IReadOnlyList<string> SpaceCodes, IReadOnlyList<Photo> Photos);

    public static IQueryable<PublicProject> VisibleProjects(RevestikDbContext db) =>
        db.Set<PublicProject>().Where(p => p.IsPublished &&
            p.Translations.Any(t => t.LanguageCode == "es" && t.Name != "" && t.Description != "") &&
            p.Images.Any(i => i.Role == "ProjectCover"));

    public static async Task<Page> ListAsync(
        RevestikDbContext db, string? language, string? typeCode,
        string? spaceCode, int pageNumber, CancellationToken ct)
    {
        var query = VisibleProjects(db).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(typeCode))
            query = query.Where(p => p.TypeCode == typeCode);
        if (!string.IsNullOrWhiteSpace(spaceCode))
            query = query.Where(p => p.Spaces.Any(s => s.SpaceCode == spaceCode));

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(p => p.DisplayOrder.HasValue ? 0 : 1)
            .ThenBy(p => p.DisplayOrder)
            .ThenByDescending(p => p.CreatedAtUtc)
            .ThenBy(p => p.Id)
            .Skip((pageNumber - 1) * PageSize).Take(PageSize)
            .Include(p => p.Translations).Include(p => p.Spaces).Include(p => p.Images)
            .AsSplitQuery().ToListAsync(ct);
        return new Page(pageNumber, PageSize, total,
            rows.Select(p => ToPreview(p, language)).ToList());
    }

    public static async Task<IReadOnlyList<Preview>> FeaturedAsync(
        RevestikDbContext db, string? language, CancellationToken ct)
    {
        var query = VisibleProjects(db).AsNoTracking();
        // Mirror Acabados: manual selection wins; only when absent do we fall back.
        var projects = await query.Where(p => p.FeaturedOrder.HasValue)
            .OrderBy(p => p.FeaturedOrder).ThenBy(p => p.Id).Take(3)
            .Include(p => p.Translations).Include(p => p.Spaces).Include(p => p.Images)
            .AsSplitQuery().ToListAsync(ct);

        if (projects.Count == 0)
        {
            projects = await query.OrderByDescending(p => p.CreatedAtUtc).ThenBy(p => p.Id)
                .Take(3)
                .Include(p => p.Translations).Include(p => p.Spaces).Include(p => p.Images)
                .AsSplitQuery().ToListAsync(ct);
        }
        return projects.Select(p => ToPreview(p, language)).ToList();
    }

    public static async Task<Detail?> GetAsync(
        RevestikDbContext db, string slug, string? language, CancellationToken ct)
    {
        var project = await VisibleProjects(db).AsNoTracking()
            .Where(p => p.Slug == slug)
            .Include(p => p.Translations).Include(p => p.Spaces)
            .Include(p => p.Images).ThenInclude(i => i.Tags)
            .ThenInclude(t => t.Product).ThenInclude(p => p!.Translations)
            .AsSplitQuery().SingleOrDefaultAsync(ct);
        if (project is null) return null;

        var referenced = project.Images.SelectMany(i => i.Tags)
            .Where(t => t.ProductId.HasValue).Select(t => t.ProductId!.Value)
            .Distinct().ToArray();
        var available = referenced.Length == 0
            ? new HashSet<int>()
            : (await PublicCatalogVisibility.VisibleProducts(db).AsNoTracking()
                .Where(p => referenced.Contains(p.Id) &&
                    db.Set<Revestik.Api.Models.PublicCatalog.PublicCatalogImage>()
                        .Any(i => i.ProductId == p.Id && i.Role == "ProductCover"))
                .Select(p => p.Id).ToListAsync(ct))
                .ToHashSet();

        var lang = NormalizeLanguage(language);
        var translation = GetText(project, lang);
        var photos = project.Images
            .OrderByDescending(i => i.Role == "ProjectCover")
            .ThenBy(i => i.DisplayOrder).ThenBy(i => i.Id)
            .Take(5)
            .Select(i => new Photo(i.Id, i.Role, i.DisplayOrder, i.Width, i.Height,
                Alt(i, lang, translation.Name),
                i.Tags.OrderBy(t => t.DisplayOrder).ThenBy(t => t.Id).Take(5)
                    .Select(t => ToHotspot(t, lang, available)).ToList()))
            .ToList();

        return new Detail(project.Id, project.Slug, project.TypeCode, project.Location,
            translation.Name, translation.Description,
            string.IsNullOrWhiteSpace(translation.SeoTitle) ? $"{translation.Name} | REVESTIK" : translation.SeoTitle,
            string.IsNullOrWhiteSpace(translation.SeoDescription) ? translation.Description : translation.SeoDescription,
            project.Spaces.Select(s => s.SpaceCode).OrderBy(s => s, StringComparer.Ordinal).ToList(), photos);
    }

    private static Preview ToPreview(PublicProject p, string? language)
    {
        var lang = NormalizeLanguage(language);
        var translation = GetText(p, lang);
        var cover = p.Images.Where(i => i.Role == "ProjectCover")
            .OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).First();
        return new Preview(p.Id, p.Slug, p.TypeCode, p.Location, translation.Name,
            translation.Description, cover.Id, Alt(cover, lang, translation.Name),
            p.Spaces.Select(s => s.SpaceCode).OrderBy(s => s, StringComparer.Ordinal).ToList());
    }

    private static Hotspot ToHotspot(PublicProjectImageTag t, string language, HashSet<int> available)
    {
        var isAvailable = t.ProductId.HasValue && available.Contains(t.ProductId.Value);
        var translatedName = isAvailable
            ? t.Product?.Translations.FirstOrDefault(x => x.LanguageCode == language)?.Name
                ?? t.Product?.Translations.FirstOrDefault(x => x.LanguageCode == "es")?.Name
            : null;
        var snapshot = language switch
        {
            "en" => t.ProductNameSnapshotEn ?? t.ProductNameSnapshotEs,
            "de" => t.ProductNameSnapshotDe ?? t.ProductNameSnapshotEs,
            _ => t.ProductNameSnapshotEs
        };
        var name = !string.IsNullOrWhiteSpace(translatedName) ? translatedName : snapshot;
        var custom = language switch
        {
            "en" => t.CustomLabelEn ?? t.CustomLabelEs,
            "de" => t.CustomLabelDe ?? t.CustomLabelEs,
            _ => t.CustomLabelEs
        };
        var label = !string.IsNullOrWhiteSpace(custom) ? custom : name;
        return new Hotspot(t.Id, t.AnchorX, t.AnchorY, t.LabelX, t.LabelY,
            t.DisplayOrder, label, name, isAvailable,
            isAvailable ? t.Product!.Slug : null);
    }

    private static PublicProjectTranslation GetText(PublicProject p, string language) =>
        p.Translations.FirstOrDefault(x => x.LanguageCode == language &&
            !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.Description))
        ?? p.Translations.First(x => x.LanguageCode == "es");

    private static string Alt(PublicProjectImage i, string language, string fallback) =>
        (language switch
        {
            "en" => i.AltEn,
            "de" => i.AltDe,
            _ => i.AltEs
        }) is { Length: > 0 } localized ? localized : i.AltEs ?? fallback;

    private static string NormalizeLanguage(string? language) =>
        language is "en" or "de" ? language : "es";
}
