using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Services.Audit;
using Revestik.Api.Services.PublicCatalog;
using Revestik.Shared.PublicSite;

namespace Revestik.Api.Endpoints.PublicCatalog;

/// <summary>
/// Seven fixed editorial sections of the public Solutions page. Read is anonymous;
/// writes are Administrator-only, antiforgery-protected and concurrency-controlled.
/// Only the hero can reference its own image; no Home or internal records are exposed.
/// </summary>
public static class PublicSolutionsSectionEndpoints
{
    private const string CacheTag = "revestik-public-solutions";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapPublicSolutionsSectionEndpoints(this IEndpointRouteBuilder app)
    {
        var publicGroup = app.MapGroup("/api/public/site/solutions-sections")
            .AllowAnonymous().WithTags("Public site");
        publicGroup.MapGet("", Read)
            .CacheOutput(policy => policy.Expire(TimeSpan.FromMinutes(10)).Tag(CacheTag));
        publicGroup.MapGet("/hero/media", ReadHeroImage);

        var admin = app.MapGroup("/api/admin/public-site/solutions-sections")
            .RequireAuthorization(PolicyNames.AdministratorOnly)
            .WithTags("Public site administration");
        admin.MapGet("", Read);
        admin.MapGet("/hero/media", ReadHeroImage);
        admin.MapPut("/{key}", Save).AddEndpointFilter<AntiforgeryValidationFilter>();
        return app;
    }

    private static async Task<IResult> Read(RevestikDbContext db, CancellationToken ct) =>
        Results.Ok((await db.Set<PublicSolutionsSection>().AsNoTracking()
                .OrderBy(x => x.Key).ToListAsync(ct)).Select(ToDto).ToList());

    private static SolutionsSectionDto ToDto(PublicSolutionsSection current) => new()
    {
        Key = current.Key,
        Version = Convert.ToBase64String(current.RowVersion),
        HasImage = current.Key == "hero" && current.ImageStorageKey is not null,
        Es = JsonSerializer.Deserialize<SolutionsSectionCopy>(current.ContentEs, Json) ?? new(),
        En = current.ContentEn is null ? null : JsonSerializer.Deserialize<SolutionsSectionCopy>(current.ContentEn, Json),
        De = current.ContentDe is null ? null : JsonSerializer.Deserialize<SolutionsSectionCopy>(current.ContentDe, Json)
    };

    private static async Task<IResult> ReadHeroImage(
        RevestikDbContext db, PublicCatalogMediaStorage storage, CancellationToken ct)
    {
        var media = await db.PublicSolutionsSections.AsNoTracking()
            .Where(x => x.Key == "hero")
            .Select(x => new { x.ImageStorageKey, x.ImageContentType })
            .SingleOrDefaultAsync(ct);
        if (media?.ImageStorageKey is null ||
            media.ImageContentType is not ("image/jpeg" or "image/png" or "image/webp"))
            return Results.NotFound();
        try
        {
            return Results.File(await storage.ReadActiveAsync(media.ImageStorageKey, ct), media.ImageContentType);
        }
        catch (FileNotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> Save(string key, SolutionsSectionWrite request,
        RevestikDbContext db, PublicCatalogMediaStorage storage,
        IOutputCacheStore cache, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!SolutionsSectionDefaults.IsKnown(key) || request is null || request.Version is null ||
            !SolutionsSectionDefaults.ValidCopy(key, request.Es, required: true) ||
            !SolutionsSectionDefaults.ValidCopy(key, request.En, required: false) ||
            !SolutionsSectionDefaults.ValidCopy(key, request.De, required: false) ||
            (key != "hero" && (request.ImageToken is not null || request.RemoveImage)) ||
            (request.ImageToken is not null &&
                (request.ImageToken.Length != 32 || !request.ImageToken.All(Uri.IsHexDigit))) ||
            (request.RemoveImage && request.ImageToken is not null))
            return Results.BadRequest(new { message = "Revisá los textos y las traducciones de Soluciones." });

        // A translation is either complete or omitted. Clearing every value is a controlled ES fallback.
        var en = SolutionsSectionDefaults.HasTranslation(request.En) ? request.En : null;
        var de = SolutionsSectionDefaults.HasTranslation(request.De) ? request.De : null;
        var esJson = JsonSerializer.Serialize(request.Es, Json);
        var enJson = en is null ? null : JsonSerializer.Serialize(en, Json);
        var deJson = de is null ? null : JsonSerializer.Serialize(de, Json);
        if (esJson.Length > 6000 || enJson?.Length > 6000 || deJson?.Length > 6000)
            return Results.BadRequest(new { message = "El contenido supera el tamaño permitido." });

        // Use the same staged-media validation as existing public heroes, but not their SQL slots.
        PublicCatalogValidatedMedia? media = null;
        if (request.ImageToken is not null)
        {
            try { media = await storage.InspectPendingAsync(request.ImageToken, "ProductCover", ct); }
            catch (Exception e) when (e is FileNotFoundException or InvalidDataException or IOException or ArgumentException)
            { return Results.BadRequest(new { message = "La fotografía no es válida o ya no está disponible." }); }
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var current = await db.Set<PublicSolutionsSection>().SingleOrDefaultAsync(x => x.Key == key, ct);
        if ((current is null && request.Version.Length > 0) ||
            (current is not null && request.Version != Convert.ToBase64String(current.RowVersion)))
            return Results.Conflict(new { message = "Esta sección cambió en otra sesión. Recargá la página." });

        if (current is null)
        {
            current = new PublicSolutionsSection { Key = key };
            db.Set<PublicSolutionsSection>().Add(current);
        }
        string? committedKey = null;
        var persisted = false;
        try
        {
            if (request.ImageToken is not null)
                committedKey = await storage.CommitAsync(request.ImageToken, ct);
            current.ContentEs = esJson;
            current.ContentEn = enJson;
            current.ContentDe = deJson;
            if (key == "hero")
            {
                if (request.RemoveImage)
                {
                    current.ImageStorageKey = null;
                    current.ImageContentType = null;
                }
                if (committedKey is not null)
                {
                    current.ImageStorageKey = committedKey;
                    current.ImageContentType = media!.ContentType;
                }
            }
            current.UpdatedAtUtc = DateTime.UtcNow;
            SecurityAudit.Record(db, "PublicSolutionsSectionSaved", "PublicSolutionsSection", key,
                user.FindFirstValue(ClaimTypes.NameIdentifier));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            persisted = true;
            if (request.ImageToken is not null)
            {
                try { storage.DeletePending(request.ImageToken); }
                catch (IOException) { /* The existing media cleanup worker retries. */ }
            }
            await cache.EvictByTagAsync(CacheTag, ct);
            return Results.Ok(ToDto(current));
        }
        catch (DbUpdateConcurrencyException)
        {
            if (committedKey is not null) storage.DeleteActive(committedKey);
            return Results.Conflict(new { message = "Esta sección cambió en otra sesión." });
        }
        catch (DbUpdateException)
        {
            if (committedKey is not null) storage.DeleteActive(committedKey);
            return Results.Conflict(new { message = "No se pudo guardar por un conflicto de datos." });
        }
        catch
        {
            // Never delete the previous photograph; it remains referenced until SQL commits.
            if (!persisted && committedKey is not null)
                storage.DeleteActive(committedKey);
            throw;
        }
    }

}
