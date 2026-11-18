using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Models.PublicProjects;
using Revestik.Api.Services.Audit;
using Revestik.Api.Services.PublicCatalog;
using Revestik.Api.Services.PublicProjects;

namespace Revestik.Api.Endpoints.PublicCatalog;

/// <summary>
/// Administrator-only visual tags for public-project photographs. No operational
/// inventory product, project, customer or financial data is accessed.
/// </summary>
public static class PublicProjectTagEndpoints
{
    public sealed record TagWrite(int? ProductId,
        decimal AnchorX, decimal AnchorY, decimal LabelX, decimal LabelY,
        int DisplayOrder, string? CustomLabelEs, string? CustomLabelEn,
        string? CustomLabelDe, string? Version);

    public static IEndpointRouteBuilder MapPublicProjectTagEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/public-projects")
            .RequireAuthorization(PolicyNames.AdministratorOnly)
            .WithTags("Public project material tags");
        admin.MapGet("/products", SearchProducts);
        admin.MapGet("/images/{imageId:int}/tags", ListTags);
        admin.MapPost("/images/{imageId:int}/tags", CreateTag)
            .AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPut("/images/{imageId:int}/tags/{tagId:int}", UpdateTag)
            .AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapDelete("/images/{imageId:int}/tags/{tagId:int}", DeleteTag)
            .AddEndpointFilter<AntiforgeryValidationFilter>();
        return app;
    }

    private static IResult Bad(string message) => Results.BadRequest(new { message });
    private static IResult Conflict(string message) => Results.Conflict(new { message });
    private static string? Actor(ClaimsPrincipal user) =>
        user.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);

    private static string Version(byte[] bytes) => Convert.ToBase64String(bytes);
    private static bool Matches(string? supplied, byte[] actual)
    {
        if (string.IsNullOrWhiteSpace(supplied)) return false;
        try { return Convert.FromBase64String(supplied).AsSpan().SequenceEqual(actual); }
        catch (FormatException) { return false; }
    }

    private static IQueryable<PublicCatalogProduct> SelectableProducts(RevestikDbContext db) =>
        PublicCatalogVisibility.VisibleProducts(db)
            .Where(p => p.Translations.Any(t => t.LanguageCode == "es" && t.Name != "") &&
                db.Set<PublicCatalogImage>().Any(i =>
                    i.ProductId == p.Id && i.Role == "ProductCover"));

    public static bool ValidTagWrite(TagWrite? input)
    {
        if (input is null || input.DisplayOrder < 0 ||
            input.AnchorX is < 0 or > 1 || input.AnchorY is < 0 or > 1 ||
            input.LabelX is < 0 or > 1 || input.LabelY is < 0 or > 1 ||
            input.CustomLabelEs?.Length > 120 || input.CustomLabelEn?.Length > 120 ||
            input.CustomLabelDe?.Length > 120) return false;
        return new[] { input.AnchorX, input.AnchorY, input.LabelX, input.LabelY }
            .All(value => decimal.Round(value, 4) == value);
    }

    private static string? ShortText(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static void Assign(PublicProjectImageTag tag, TagWrite input)
    {
        tag.AnchorX = input.AnchorX;
        tag.AnchorY = input.AnchorY;
        tag.LabelX = input.LabelX;
        tag.LabelY = input.LabelY;
        tag.DisplayOrder = input.DisplayOrder;
        tag.CustomLabelEs = ShortText(input.CustomLabelEs);
        tag.CustomLabelEn = ShortText(input.CustomLabelEn);
        tag.CustomLabelDe = ShortText(input.CustomLabelDe);
    }

    private static object Dto(PublicProjectImageTag tag) => new
    {
        tag.Id, tag.ImageId, tag.ProductId, tag.AnchorX, tag.AnchorY,
        tag.LabelX, tag.LabelY, tag.DisplayOrder,
        tag.CustomLabelEs, tag.CustomLabelEn, tag.CustomLabelDe,
        tag.ProductNameSnapshotEs, tag.ProductNameSnapshotEn, tag.ProductNameSnapshotDe,
        Version = Version(tag.RowVersion)
    };

    private static async Task<IResult> SearchProducts(
        string? search, RevestikDbContext db, CancellationToken ct)
    {
        if (search is null || search.Length > 100) return Bad("Search must be between 2 and 100 characters.");
        var term = search.Trim();
        if (term.Length < 2) return Results.Ok(Array.Empty<object>());
        var products = await SelectableProducts(db).AsNoTracking()
            .Where(p => p.Slug.Contains(term) || p.Translations.Any(t => t.Name.Contains(term)))
            .OrderBy(p => p.Translations.Where(t => t.LanguageCode == "es")
                .Select(t => t.Name).FirstOrDefault())
            .ThenBy(p => p.Id).Take(20)
            .Select(p => new
            {
                p.Id, p.Slug,
                Name = p.Translations.Where(t => t.LanguageCode == "es")
                    .Select(t => t.Name).FirstOrDefault(),
                Category = p.SubcategoryLinks.OrderBy(l => l.SubcategoryId)
                    .Select(l => l.Subcategory.Category.Name).FirstOrDefault(),
                CoverImageId = db.Set<PublicCatalogImage>()
                    .Where(i => i.ProductId == p.Id && i.Role == "ProductCover")
                    .Select(i => (int?)i.Id).FirstOrDefault()
            }).ToListAsync(ct);
        return Results.Ok(products);
    }

    private static async Task<IResult> ListTags(int imageId, RevestikDbContext db,
        CancellationToken ct)
    {
        if (!await db.Set<PublicProjectImage>().AnyAsync(i => i.Id == imageId, ct))
            return Results.NotFound();
        var tags = await db.Set<PublicProjectImageTag>().AsNoTracking()
            .Where(t => t.ImageId == imageId)
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Id).ToListAsync(ct);
        return Results.Ok(tags.Select(Dto).ToArray());
    }

    // Called inside the create transaction with Serializable isolation so the five-tag
    // limit is enforced by the API, not only by the visual editor.
    public static async Task<bool> HasTagCapacityAsync(RevestikDbContext db,
        int imageId, CancellationToken ct) =>
        await db.Set<PublicProjectImageTag>().CountAsync(t => t.ImageId == imageId, ct) < 5;

    private static async Task<IResult> CreateTag(int imageId, TagWrite input,
        RevestikDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!ValidTagWrite(input) || input.ProductId is not > 0)
            return Bad("A published public-catalog product is required.");
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var image = await db.Set<PublicProjectImage>().SingleOrDefaultAsync(i => i.Id == imageId, ct);
            if (image is null) return Results.NotFound();

            if (!await HasTagCapacityAsync(db, imageId, ct))
                return Bad("A photograph supports at most five material tags.");
            var product = await SelectableProducts(db).Include(p => p.Translations)
                .SingleOrDefaultAsync(p => p.Id == input.ProductId, ct);
            if (product is null) return Bad("Select a currently published product from Acabados.");
            var tag = new PublicProjectImageTag { ImageId = imageId, ProductId = product.Id };
            Assign(tag, input);
            PublicProjectTagSnapshots.CopyNames(product, tag);
            db.Set<PublicProjectImageTag>().Add(tag);
            SecurityAudit.Record(db, "PublicProjectImageTagAdded", "PublicProjectImageTag",
                null, Actor(user));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Created($"/api/admin/public-projects/images/{imageId}/tags/{tag.Id}", Dto(tag));
        }
        catch (DbUpdateConcurrencyException) { return Conflict("Material tag changed. Reload."); }
        catch (DbUpdateException) { return Conflict("Material tag or product changed. Reload."); }
    }

    private static async Task<IResult> UpdateTag(int imageId, int tagId, TagWrite input,
        RevestikDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!ValidTagWrite(input) || string.IsNullOrWhiteSpace(input.Version))
            return Bad("Invalid material tag or concurrency version.");
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var image = await db.Set<PublicProjectImage>().SingleOrDefaultAsync(i => i.Id == imageId, ct);
            if (image is null) return Results.NotFound();

            var tag = await db.Set<PublicProjectImageTag>()
                .SingleOrDefaultAsync(t => t.Id == tagId && t.ImageId == imageId, ct);
            if (tag is null) return Results.NotFound();
            if (!Matches(input.Version, tag.RowVersion)) return Conflict("Material tag changed. Reload.");
            if (input.ProductId != tag.ProductId)
            {
                if (input.ProductId is not > 0)
                    return Bad("A linked product cannot be cleared manually.");
                var product = await SelectableProducts(db).Include(p => p.Translations)
                    .SingleOrDefaultAsync(p => p.Id == input.ProductId, ct);
                if (product is null) return Bad("Select a currently published product from Acabados.");
                tag.ProductId = product.Id;
                PublicProjectTagSnapshots.CopyNames(product, tag);
            }
            Assign(tag, input);
            SecurityAudit.Record(db, "PublicProjectImageTagUpdated", "PublicProjectImageTag",
                tag.Id.ToString(), Actor(user));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(Dto(tag));
        }
        catch (DbUpdateConcurrencyException) { return Conflict("Material tag changed. Reload."); }
        catch (DbUpdateException) { return Conflict("Material tag or product changed. Reload."); }
    }

    private static async Task<IResult> DeleteTag(int imageId, int tagId, string version,
        RevestikDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(version)) return Bad("Material tag version is required.");
        var tag = await db.Set<PublicProjectImageTag>()
            .SingleOrDefaultAsync(t => t.Id == tagId && t.ImageId == imageId, ct);
        if (tag is null) return Results.NotFound();
        if (!Matches(version, tag.RowVersion)) return Conflict("Material tag changed. Reload.");
        db.Set<PublicProjectImageTag>().Remove(tag);
        SecurityAudit.Record(db, "PublicProjectImageTagDeleted", "PublicProjectImageTag",
            tag.Id.ToString(), Actor(user));
        try
        {
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }
        catch (DbUpdateConcurrencyException) { return Conflict("Material tag changed. Reload."); }
        catch (DbUpdateException) { return Conflict("Material tag could not be deleted."); }
    }
}
