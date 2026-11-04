using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Extensions;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Services.Audit;
using Revestik.Api.Services.PublicCatalog;
using Revestik.Shared.PublicSite;

namespace Revestik.Api.Endpoints.PublicCatalog;

public static class PublicHomeSectionEndpoints
{
    private static readonly string[] Keys = ["solutions", "advantages", "contact", "products"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapPublicHomeSectionEndpoints(this IEndpointRouteBuilder app)
    {
        var publicGroup = app.MapGroup("/api/public/site/home-sections").AllowAnonymous().WithTags("Public site");
        publicGroup.MapGet("", async (RevestikDbContext db, CancellationToken ct) =>
            Results.Ok((await db.PublicHomeSections.AsNoTracking().ToListAsync(ct)).Select(ToDto).ToList()));
        publicGroup.MapGet("/solutions/media", ReadSolutionsImage);
        var admin = app.MapGroup("/api/admin/public-site/home-sections")
            .RequireAuthorization(PolicyNames.AdministratorOnly).WithTags("Public site administration");
        admin.MapGet("", async (RevestikDbContext db, CancellationToken ct) =>
            Results.Ok((await db.PublicHomeSections.AsNoTracking().ToListAsync(ct)).Select(ToDto).ToList()));
        admin.MapGet("/solutions/media", ReadSolutionsImage);
        admin.MapPut("/{key}", SaveSection).AddEndpointFilter<AntiforgeryValidationFilter>();
        return app;
    }

    private static HomeSectionDto ToDto(PublicHomeSection e) => new()
    {
        Key = e.Key, Version = Convert.ToBase64String(e.RowVersion),
        Es = JsonSerializer.Deserialize<HomeSectionCopy>(e.ContentEs, Json) ?? new(),
        En = e.ContentEn is null ? null : JsonSerializer.Deserialize<HomeSectionCopy>(e.ContentEn, Json),
        De = e.ContentDe is null ? null : JsonSerializer.Deserialize<HomeSectionCopy>(e.ContentDe, Json),
        FeaturedSlugs = JsonSerializer.Deserialize<List<string>>(e.FeaturedSlugsJson, Json) ?? [],
        HasImage = e.ImageStorageKey is not null
    };

    private static async Task<IResult> ReadSolutionsImage(RevestikDbContext db, PublicCatalogMediaStorage storage, CancellationToken ct)
    {
        var media = await db.PublicHomeSections.AsNoTracking().Where(x => x.Key == "solutions")
            .Select(x => new { x.ImageStorageKey, x.ImageContentType }).SingleOrDefaultAsync(ct);
        if (media?.ImageStorageKey is null || media.ImageContentType is not ("image/jpeg" or "image/png" or "image/webp"))
            return Results.NotFound();
        try { return Results.File(await storage.ReadActiveAsync(media.ImageStorageKey, ct), media.ImageContentType); }
        catch (FileNotFoundException) { return Results.NotFound(); }
    }

    private static bool ValidCopy(HomeSectionCopy? copy, bool required, bool advantages)
    {
        if (copy is null) return !required;
        if (copy.Eyebrow?.Length > 90 || copy.Title?.Length > 180 || copy.Description?.Length > 1000) return false;
        if (required && (string.IsNullOrWhiteSpace(copy.Title) || string.IsNullOrWhiteSpace(copy.Description))) return false;
        if (copy.Cards is null || copy.Cards.Count > 4) return false;
        if (required && advantages && copy.Cards.Count != 4) return false;
        return copy.Cards.All(x => x is not null && x.Title is { Length: <= 180 } &&
            x.Description is { Length: <= 500 } &&
            (!required || !advantages || (!string.IsNullOrWhiteSpace(x.Title) && !string.IsNullOrWhiteSpace(x.Description))));
    }

    private static async Task<IResult> SaveSection(string key, HomeSectionWrite request,
        RevestikDbContext db, PublicCatalogMediaStorage storage, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!Keys.Contains(key, StringComparer.Ordinal) || request is null || request.Version is null ||
            !ValidCopy(request.Es, true, key == "advantages") ||
            !ValidCopy(request.En, false, false) || !ValidCopy(request.De, false, false) ||
            request.FeaturedSlugs is null || request.FeaturedSlugs.Count > 5 ||
            request.FeaturedSlugs.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 120) ||
            request.FeaturedSlugs.Distinct(StringComparer.Ordinal).Count() != request.FeaturedSlugs.Count ||
            (key != "products" && request.FeaturedSlugs.Count != 0) ||
            (key != "solutions" && (request.ImageToken is not null || request.RemoveImage)) ||
            (request.RemoveImage && request.ImageToken is not null))
            return Results.BadRequest(new { message = "Revisá los textos, imágenes o productos destacados." });

        var serializedEs = JsonSerializer.Serialize(request.Es, Json);
        var serializedEn = request.En is null ? null : JsonSerializer.Serialize(request.En, Json);
        var serializedDe = request.De is null ? null : JsonSerializer.Serialize(request.De, Json);
        var featuredJson = JsonSerializer.Serialize(request.FeaturedSlugs, Json);
        if (serializedEs.Length > 6000 || serializedEn?.Length > 6000 || serializedDe?.Length > 6000 || featuredJson.Length > 1000)
            return Results.BadRequest(new { message = "El contenido supera el tamaño permitido." });

        if (key == "products" && request.FeaturedSlugs.Count > 0)
        {
            var approved = await db.PublicCatalogProducts.AsNoTracking()
                .Where(p => request.FeaturedSlugs.Contains(p.Slug) && p.IsPublished && !p.IsArchived &&
                    p.SubcategoryLinks.Any(l => l.Subcategory.IsActive && l.Subcategory.Category.IsActive))
                .Select(p => p.Slug).ToListAsync(ct);
            if (approved.Count != request.FeaturedSlugs.Count)
                return Results.BadRequest(new { message = "Solo se pueden destacar productos publicados y disponibles." });
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var current = await db.PublicHomeSections.SingleOrDefaultAsync(x => x.Key == key, ct);
        if ((current is null && request.Version.Length > 0) ||
            (current is not null && Convert.ToBase64String(current.RowVersion) != request.Version))
            return Results.Conflict(new { message = "La sección cambió en otra sesión." });

        PublicCatalogValidatedMedia? media = null;
        if (request.ImageToken is not null)
        {
            try { media = await storage.InspectPendingAsync(request.ImageToken, "ProductCover", ct); }
            catch (Exception e) when (e is FileNotFoundException or InvalidDataException or IOException or ArgumentException)
            { return Results.BadRequest(new { message = "La fotografía no es válida o ya no está disponible." }); }
        }

        string? newKey = null;
        try
        {
            if (request.ImageToken is not null)
                newKey = await storage.CommitAsync(request.ImageToken, ct);
            if (current is null)
            {
                current = new PublicHomeSection { Key = key };
                db.PublicHomeSections.Add(current);
            }
            current.ContentEs = serializedEs;
            current.ContentEn = serializedEn;
            current.ContentDe = serializedDe;
            current.FeaturedSlugsJson = featuredJson;
            if (request.RemoveImage) { current.ImageStorageKey = null; current.ImageContentType = null; }
            if (newKey is not null) { current.ImageStorageKey = newKey; current.ImageContentType = media!.ContentType; }
            current.UpdatedAtUtc = DateTime.UtcNow;
            SecurityAudit.Record(db, "PublicHomeSectionSaved", "PublicHomeSection", key,
                user.FindFirstValue(ClaimTypes.NameIdentifier));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            if (request.ImageToken is not null)
            {
                try { storage.DeletePending(request.ImageToken); } catch (IOException) { /* cleanup handles it */ }
            }
            return Results.Ok(ToDto(current));
        }
        catch (DbUpdateConcurrencyException) { if (newKey is not null) storage.DeleteActive(newKey); return Results.Conflict(); }
        catch (DbUpdateException) { if (newKey is not null) storage.DeleteActive(newKey); return Results.Conflict(); }
        catch { if (newKey is not null) storage.DeleteActive(newKey); throw; }
    }
}
