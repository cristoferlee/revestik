using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models.PublicProjects;
using Revestik.Api.Services.PublicCatalog;
using Revestik.Api.Services.PublicProjects;

namespace Revestik.Api.Endpoints.PublicCatalog;

// Extends the existing public catalog endpoint namespace; intentionally read-only.
// Administration, uploads and mutations will be added in the next 01B increment.
public static class PublicProjectReadEndpoints
{
    public static IEndpointRouteBuilder MapPublicProjectReadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/public/projects")
            .AllowAnonymous().WithTags("Public projects");
        group.MapGet("", List);
        group.MapGet("/featured", Featured);
        group.MapGet("/images/{id:int}", Image);
        group.MapGet("/{slug}", Detail);
        return app;
    }

    private static async Task<IResult> List(
        RevestikDbContext db, string? language, string? type, string? space,
        int? page, CancellationToken ct)
    {
        var number = page ?? 1;
        if (number is < 1 or > 10000 ||
            (!string.IsNullOrEmpty(type) && !PublicProjectClassification.IsKnownType(type)) ||
            (!string.IsNullOrEmpty(space) && !PublicProjectClassification.IsKnownSpace(space)))
            return Results.BadRequest(new { message = "Invalid project filter." });
        return Results.Ok(await PublicProjectReader.ListAsync(db, language, type, space, number, ct));
    }

    private static async Task<IResult> Featured(
        RevestikDbContext db, string? language, CancellationToken ct) =>
        Results.Ok(await PublicProjectReader.FeaturedAsync(db, language, ct));

    private static async Task<IResult> Detail(
        string slug, RevestikDbContext db, string? language, CancellationToken ct)
    {
        if (slug.Length is < 1 or > 160 || slug[0] == '-' || slug[^1] == '-' ||
            !slug.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-'))
            return Results.NotFound();
        var project = await PublicProjectReader.GetAsync(db, slug, language, ct);
        return project is null ? Results.NotFound() : Results.Ok(project);
    }

    private static async Task<IResult> Image(
        int id, RevestikDbContext db, PublicCatalogMediaStorage storage, CancellationToken ct)
    {
        var image = await db.Set<PublicProjectImage>().AsNoTracking()
            .Where(i => i.Id == id && i.Project.IsPublished &&
                i.Project.Translations.Any(t => t.LanguageCode == "es" && t.Name != "" && t.Description != "") &&
                i.Project.Images.Any(x => x.Role == "ProjectCover"))
            .Select(i => new { i.StorageKey, i.ContentType }).SingleOrDefaultAsync(ct);
        if (image is null) return Results.NotFound();
        try
        {
            return Results.File(await storage.ReadActiveAsync(image.StorageKey, ct), image.ContentType);
        }
        catch (FileNotFoundException) { return Results.NotFound(); }
        catch (DirectoryNotFoundException) { return Results.NotFound(); }
    }
}
