using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Services.Audit;
using Revestik.Api.Services.PublicCatalog;
using Revestik.Api.Extensions;

namespace Revestik.Api.Endpoints.PublicCatalog;

public static class PublicHomeHeroEndpoints
{
    public static IEndpointRouteBuilder MapPublicHomeHeroEndpoints(this IEndpointRouteBuilder app)
    {
        var publicGroup = app.MapGroup("/api/public/site/home-hero").AllowAnonymous().WithTags("Public site");
        publicGroup.MapGet("", async (RevestikDbContext db, CancellationToken ct) =>
            Results.Ok((await db.PublicHomeHeroSlides.AsNoTracking().OrderBy(x => x.Slot).ToListAsync(ct)).Select(ToDto)));
        publicGroup.MapGet("/media/{slot:int}", (int slot, RevestikDbContext db, PublicCatalogMediaStorage storage, CancellationToken ct) =>
            ReadImage(slot, db, storage, ct));

        var admin = app.MapGroup("/api/admin/public-site/home-hero")
            .RequireAuthorization(PolicyNames.AdministratorOnly).WithTags("Public site administration");
        admin.MapGet("", async (RevestikDbContext db, CancellationToken ct) =>
            Results.Ok((await db.PublicHomeHeroSlides.AsNoTracking().OrderBy(x => x.Slot).ToListAsync(ct)).Select(ToDto)));
        admin.MapGet("/media/{slot:int}", (int slot, RevestikDbContext db, PublicCatalogMediaStorage storage, CancellationToken ct) =>
            ReadImage(slot, db, storage, ct));
        admin.MapPut("/{slot:int}", SaveSlide).AddEndpointFilter<AntiforgeryValidationFilter>();
        return app;
    }

    private static object ToDto(PublicHomeHeroSlide x) => new
    {
        x.Slot, x.HeadingEs, x.HeadingEn, x.HeadingDe,
        x.DescriptionEs, x.DescriptionEn, x.DescriptionDe,
        HasImage = x.ImageStorageKey is not null,
        Version = Convert.ToBase64String(x.RowVersion)
    };

    private static async Task<IResult> ReadImage(int slot, RevestikDbContext db, PublicCatalogMediaStorage storage, CancellationToken ct)
    {
        if (slot is < 1 or > 3) return Results.NotFound();
        var image = await db.PublicHomeHeroSlides.AsNoTracking().Where(x => x.Slot == slot)
            .Select(x => new { x.ImageStorageKey, x.ImageContentType }).SingleOrDefaultAsync(ct);
        if (image?.ImageStorageKey is null || image.ImageContentType is not ("image/jpeg" or "image/png" or "image/webp"))
            return Results.NotFound();
        try { return Results.File(await storage.ReadActiveAsync(image.ImageStorageKey, ct), image.ImageContentType); }
        catch (FileNotFoundException) { return Results.NotFound(); }
    }

    public sealed record SlideWrite(
        string Version, string HeadingEs, string? HeadingEn, string? HeadingDe,
        string DescriptionEs, string? DescriptionEn, string? DescriptionDe,
        string? ImageToken, bool RemoveImage);

    private static bool IsValid(SlideWrite r) =>
        r.HeadingEs is { Length: > 0 and <= 180 } &&
        !string.IsNullOrWhiteSpace(r.HeadingEs) &&
        r.DescriptionEs is { Length: > 0 and <= 1000 } &&
        !string.IsNullOrWhiteSpace(r.DescriptionEs) &&
        (r.HeadingEn is null || r.HeadingEn.Length <= 180) &&
        (r.HeadingDe is null || r.HeadingDe.Length <= 180) &&
        (r.DescriptionEn is null || r.DescriptionEn.Length <= 1000) &&
        (r.DescriptionDe is null || r.DescriptionDe.Length <= 1000) &&
        (r.ImageToken is null || (r.ImageToken.Length == 32 && r.ImageToken.All(Uri.IsHexDigit))) &&
        !(r.RemoveImage && r.ImageToken is not null);

    private static async Task<IResult> SaveSlide(int slot, SlideWrite request,
        RevestikDbContext db, PublicCatalogMediaStorage storage, ClaimsPrincipal user, CancellationToken ct)
    {
        if (slot is < 1 or > 3 || request is null || !IsValid(request))
            return Results.BadRequest(new { message = "Verificá el título y la descripción en español." });

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var slide = await db.PublicHomeHeroSlides.SingleOrDefaultAsync(x => x.Slot == slot, ct);
        if (slide is null && !string.IsNullOrEmpty(request.Version))
            return Results.Conflict(new { message = "La portada cambió. Recargá la página." });
        if (slide is not null && request.Version != Convert.ToBase64String(slide.RowVersion))
            return Results.Conflict(new { message = "La portada cambió. Recargá la página." });

        PublicCatalogValidatedMedia? media = null;
        if (request.ImageToken is not null)
        {
            try { media = await storage.InspectPendingAsync(request.ImageToken, "ProductCover", ct); }
            catch (Exception e) when (e is FileNotFoundException or InvalidDataException or IOException or ArgumentException)
            { return Results.BadRequest(new { message = "La imagen no está disponible o no es válida." }); }
        }

        var newKey = (string?)null;
        try
        {
            if (request.ImageToken is not null)
                newKey = await storage.CommitAsync(request.ImageToken, ct);
            if (slide is null)
            {
                slide = new PublicHomeHeroSlide { Slot = slot };
                db.PublicHomeHeroSlides.Add(slide);
            }
            slide.HeadingEs = request.HeadingEs.Trim();
            slide.HeadingEn = TrimOptional(request.HeadingEn);
            slide.HeadingDe = TrimOptional(request.HeadingDe);
            slide.DescriptionEs = request.DescriptionEs.Trim();
            slide.DescriptionEn = TrimOptional(request.DescriptionEn);
            slide.DescriptionDe = TrimOptional(request.DescriptionDe);
            if (request.RemoveImage)
            {
                slide.ImageStorageKey = null;
                slide.ImageContentType = null;
            }
            if (newKey is not null)
            {
                slide.ImageStorageKey = newKey;
                slide.ImageContentType = media!.ContentType;
            }
            slide.UpdatedAtUtc = DateTime.UtcNow;
            SecurityAudit.Record(db, "PublicHomeHeroSlideSaved", "PublicHomeHeroSlide", slot.ToString(),
                user.FindFirstValue(ClaimTypes.NameIdentifier));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            if (request.ImageToken is not null)
            {
                try { storage.DeletePending(request.ImageToken); } catch (IOException) { /* Stale pending file pruned by worker. */ }
            }
            // Old media is intentionally left until the active orphan cleaner confirms it is unreferenced.
            return Results.Ok(ToDto(slide));
        }
        catch (DbUpdateConcurrencyException)
        {
            if (newKey is not null) storage.DeleteActive(newKey);
            return Results.Conflict(new { message = "La portada cambió. Recargá la página." });
        }
        catch (DbUpdateException)
        {
            if (newKey is not null) storage.DeleteActive(newKey);
            return Results.Conflict(new { message = "No se pudo guardar la portada por un conflicto de datos." });
        }
        catch
        {
            if (newKey is not null) storage.DeleteActive(newKey);
            throw;
        }
    }

    private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
