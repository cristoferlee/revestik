using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Extensions;
using Revestik.Api.Models.PublicProjects;
using Revestik.Api.Services.Audit;
using Revestik.Api.Services.PublicCatalog;

namespace Revestik.Api.Endpoints.PublicCatalog;

/// <summary>
/// Administrator-only public editorial project management. These records are unrelated to
/// operational Projects, customers, inventory, quotations or sales.
/// Project photos reuse the existing media store and staged upload validation.
/// </summary>
public static class PublicProjectAdminEndpoints
{
    public sealed record TranslationWrite(string LanguageCode, string Name, string Description,
        string? SeoTitle, string? SeoDescription);
    public sealed record ProjectWrite(string TypeCode, string? Location, int? DisplayOrder,
        int? FeaturedOrder, List<string> SpaceCodes, List<TranslationWrite> Translations,
        string? Version);
    public sealed record StagedImageWrite(string Token, string Role, int DisplayOrder,
        string? AltEs, string? AltEn, string? AltDe);
    public sealed record ExistingImageWrite(int Id, string Role, int DisplayOrder,
        string? AltEs, string? AltEn, string? AltDe, string Version);
    public sealed record CreateWrite(ProjectWrite Project, List<StagedImageWrite> PendingImages);
    public sealed record ContentWrite(ProjectWrite Project, List<StagedImageWrite> PendingImages,
        List<ExistingImageWrite> ExistingImages, List<int> RemovedImageIds);
    public sealed record VersionWrite(string Version);

    public static IEndpointRouteBuilder MapPublicProjectAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/public-projects")
            .RequireAuthorization(PolicyNames.AdministratorOnly)
            .WithTags("Public projects administration");
        admin.MapGet("", List);
        admin.MapGet("/{id:int}", Detail);
        admin.MapPost("", Create).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPut("/{id:int}/content", UpdateContent).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPost("/{id:int}/publish", Publish).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPost("/{id:int}/unpublish", Unpublish).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapDelete("/{id:int}", Delete).AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapPost("/images/stage", StageImage).DisableAntiforgery()
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .RequireRateLimiting(RevestikRateLimiting.DocumentUpload);
        admin.MapGet("/images/staged/{token}", PreviewStagedImage);
        admin.MapDelete("/images/staged/{token}", DeleteStagedImage)
            .AddEndpointFilter<AntiforgeryValidationFilter>();
        admin.MapGet("/images/{id:int}/content", AdminImageContent);
        return app;
    }

    private static IResult Bad(string message) => Results.BadRequest(new { message });
    private static IResult Conflict(string message) => Results.Conflict(new { message });
    private static string? Actor(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier);
    private static string EncodeVersion(byte[] version) => Convert.ToBase64String(version);
    private static bool VersionMatches(string? value, byte[] current)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { return Convert.FromBase64String(value).AsSpan().SequenceEqual(current); }
        catch (FormatException) { return false; }
    }

    public static bool ValidProjectWrite(ProjectWrite? input)
    {
        if (input is null || !PublicProjectClassification.IsKnownType(input.TypeCode) ||
            input.Location?.Length > 240 || input.DisplayOrder is < 0 ||
            (input.FeaturedOrder is not null && input.FeaturedOrder is < 1 or > 3) ||
            input.SpaceCodes is null || input.SpaceCodes.Count > 13 ||
            input.SpaceCodes.Any(x => !PublicProjectClassification.IsKnownSpace(x)) ||
            input.SpaceCodes.Count != input.SpaceCodes.Distinct(StringComparer.Ordinal).Count() ||
            input.Translations is null || input.Translations.Count is < 1 or > 3 ||
            input.Translations.Any(t => t is null) ||
            input.Translations.Count != input.Translations.Select(t => t.LanguageCode).Distinct(StringComparer.Ordinal).Count())
            return false;
        return input.Translations.Any(t => t.LanguageCode == "es" &&
                    !string.IsNullOrWhiteSpace(t.Name) && !string.IsNullOrWhiteSpace(t.Description)) &&
            input.Translations.All(t => t.LanguageCode is "es" or "en" or "de" &&
                    !string.IsNullOrWhiteSpace(t.Name) && t.Name.Length <= 200 &&
                    !string.IsNullOrWhiteSpace(t.Description) && t.Description.Length <= 4000 &&
                    (t.SeoTitle is null || t.SeoTitle.Length <= 180) &&
                    (t.SeoDescription is null || t.SeoDescription.Length <= 320));
    }

    private static bool ImageRole(string role) => role is "ProjectCover" or "ProjectGallery";
    private static bool AltOk(string? value) => value is null || value.Length <= 300;
    private static bool ValidStaged(IReadOnlyList<StagedImageWrite>? items) =>
        items is not null && items.Count <= 5 &&
        items.All(i => i is not null && !string.IsNullOrWhiteSpace(i.Token) && ImageRole(i.Role) &&
            i.DisplayOrder >= 0 && AltOk(i.AltEs) && AltOk(i.AltEn) && AltOk(i.AltDe)) &&
        items.Select(i => i.Token).Distinct(StringComparer.Ordinal).Count() == items.Count;

    private static bool ValidExisting(IReadOnlyList<ExistingImageWrite>? items) =>
        items is not null && items.Count <= 5 &&
        items.All(i => i is not null && i.Id > 0 && ImageRole(i.Role) &&
            i.DisplayOrder >= 0 && AltOk(i.AltEs) && AltOk(i.AltEn) && AltOk(i.AltDe) &&
            !string.IsNullOrWhiteSpace(i.Version)) &&
        items.Select(i => i.Id).Distinct().Count() == items.Count;

    private static string SlugFromName(string name)
    {
        var normalized = name.Trim().Normalize(NormalizationForm.FormD);
        var slug = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsAsciiLetterLower(ch) || char.IsAsciiDigit(ch)) slug.Append(ch);
            else if (char.IsAsciiLetterUpper(ch)) slug.Append(char.ToLowerInvariant(ch));
            else if (slug.Length > 0 && slug[^1] != '-') slug.Append('-');
            if (slug.Length >= 120) break;
        }
        var value = slug.ToString().Trim('-');
        return value.Length == 0 ? "proyecto" : value;
    }

    // A slug is generated once on creation, then remains stable after editorial renames.
    public static async Task<string> GenerateUniqueSlugAsync(string spanishName,
        RevestikDbContext db, CancellationToken ct)
    {
        var baseSlug = SlugFromName(spanishName);
        for (var ordinal = 1; ordinal <= 10000; ordinal++)
        {
            var suffix = ordinal == 1 ? "" : $"-{ordinal}";
            var candidate = baseSlug[..Math.Min(baseSlug.Length, 120 - suffix.Length)].TrimEnd('-') + suffix;
            if (!await db.Set<PublicProject>().AsNoTracking().AnyAsync(p => p.Slug == candidate, ct))
                return candidate;
        }
        throw new InvalidOperationException("No public project slug is available for this name.");
    }

    private static void Assign(PublicProject project, ProjectWrite input)
    {
        project.TypeCode = input.TypeCode;
        project.Location = string.IsNullOrWhiteSpace(input.Location) ? null : input.Location.Trim();
        project.DisplayOrder = input.DisplayOrder;
        project.FeaturedOrder = input.FeaturedOrder;
        project.UpdatedAtUtc = DateTime.UtcNow;
        foreach (var old in project.Translations.Where(old =>
            !input.Translations.Any(t => t.LanguageCode == old.LanguageCode)).ToArray())
            project.Translations.Remove(old);
        foreach (var t in input.Translations)
        {
            var current = project.Translations.FirstOrDefault(x => x.LanguageCode == t.LanguageCode);
            if (current is null)
            {
                project.Translations.Add(new PublicProjectTranslation
                {
                    LanguageCode = t.LanguageCode, Name = t.Name.Trim(),
                    Description = t.Description.Trim(), SeoTitle = t.SeoTitle,
                    SeoDescription = t.SeoDescription
                });
            }
            else
            {
                current.Name = t.Name.Trim();
                current.Description = t.Description.Trim();
                current.SeoTitle = t.SeoTitle;
                current.SeoDescription = t.SeoDescription;
            }
        }
        foreach (var old in project.Spaces.Where(old => !input.SpaceCodes.Contains(old.SpaceCode)).ToArray())
            project.Spaces.Remove(old);
        foreach (var code in input.SpaceCodes.Where(code => !project.Spaces.Any(s => s.SpaceCode == code)))
            project.Spaces.Add(new PublicProjectSpace { SpaceCode = code });
    }

    // Existing catalog stage accepts ProductCover as a generic static photo role.
    // ProjectCover/ProjectGallery are *database* roles, never used to access private files.
    private static async Task<List<(StagedImageWrite Input, PublicCatalogValidatedMedia Media)>?>
        InspectAsync(List<StagedImageWrite> images, PublicCatalogMediaStorage storage, CancellationToken ct)
    {
        var result = new List<(StagedImageWrite, PublicCatalogValidatedMedia)>();
        try
        {
            foreach (var input in images)
                result.Add((input, await storage.InspectPendingAsync(input.Token, "ProductCover", ct)));
            return result;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static PublicProjectImage NewImage(int id, StagedImageWrite input,
        PublicCatalogValidatedMedia media, string storageKey) => new()
        {
            ProjectId = id, Role = input.Role, StorageKey = storageKey,
            ContentType = media.ContentType, Width = media.Width, Height = media.Height,
            FileSizeBytes = media.FileSizeBytes, DisplayOrder = input.DisplayOrder,
            AltEs = input.AltEs, AltEn = input.AltEn, AltDe = input.AltDe,
            CreatedAtUtc = DateTime.UtcNow
        };

    private static object ProjectDto(PublicProject p) => new
    {
        p.Id, p.Slug, p.TypeCode, p.Location, p.DisplayOrder, p.FeaturedOrder,
        p.IsPublished, p.CreatedAtUtc, p.UpdatedAtUtc, Version = EncodeVersion(p.RowVersion),
        SpaceCodes = p.Spaces.Select(s => s.SpaceCode).OrderBy(s => s).ToArray(),
        Translations = p.Translations.OrderBy(t => t.LanguageCode).Select(t => new
        {
            t.LanguageCode, t.Name, t.Description, t.SeoTitle, t.SeoDescription
        }).ToArray(),
        Images = p.Images.OrderByDescending(i => i.Role == "ProjectCover")
            .ThenBy(i => i.DisplayOrder).Select(i => new
            {
                i.Id, i.Role, i.DisplayOrder, i.ContentType, i.Width, i.Height,
                i.AltEs, i.AltEn, i.AltDe, TagCount = i.Tags.Count,
                Version = EncodeVersion(i.RowVersion)
            }).ToArray()
    };

    private static async Task<IResult> List(RevestikDbContext db, int? page,
        string? search, CancellationToken ct)
    {
        var number = page ?? 1;
        if (number is < 1 or > 10000 || search?.Length > 150) return Bad("Invalid paging or search.");
        var q = db.Set<PublicProject>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(p => p.Slug.Contains(search) ||
                p.Translations.Any(t => t.LanguageCode == "es" && t.Name.Contains(search)));
        var count = await q.CountAsync(ct);
        var rows = await q.OrderBy(p => p.DisplayOrder.HasValue ? 0 : 1)
            .ThenBy(p => p.DisplayOrder).ThenByDescending(p => p.CreatedAtUtc).ThenBy(p => p.Id)
            .Skip((number - 1) * 20).Take(20)
            .Select(p => new { p.Id, p.Slug, p.IsPublished, p.TypeCode,
                p.DisplayOrder, p.FeaturedOrder,
                NameEs = p.Translations.Where(t => t.LanguageCode == "es")
                    .Select(t => t.Name).FirstOrDefault(),
                CoverImageId = p.Images.Where(i => i.Role == "ProjectCover")
                    .Select(i => (int?)i.Id).FirstOrDefault(),
                ImageCount = p.Images.Count, Version = p.RowVersion })
            .ToListAsync(ct);
        return Results.Ok(new { Total = count, Page = number, PageSize = 20,
            Items = rows.Select(p => new { p.Id, p.Slug, p.IsPublished, p.TypeCode,
                p.DisplayOrder, p.FeaturedOrder, p.NameEs, p.CoverImageId,
                p.ImageCount, Version = EncodeVersion(p.Version) }) });
    }

    private static async Task<IResult> Detail(int id, RevestikDbContext db, CancellationToken ct)
    {
        var p = await db.Set<PublicProject>().AsNoTracking().Where(p => p.Id == id)
            .Include(p => p.Translations).Include(p => p.Spaces)
            .Include(p => p.Images).ThenInclude(i => i.Tags)
            .AsSplitQuery().SingleOrDefaultAsync(ct);
        return p is null ? Results.NotFound() : Results.Ok(ProjectDto(p));
    }

    private static async Task<IResult> Create(CreateWrite r, RevestikDbContext db,
        PublicCatalogMediaStorage storage, ClaimsPrincipal user, CancellationToken ct)
    {
        if (r?.Project is null || !ValidProjectWrite(r.Project) ||
            !ValidStaged(r.PendingImages) || r.PendingImages.Count is < 1 or > 5 ||
            r.PendingImages.Count(i => i.Role == "ProjectCover") != 1)
            return Bad("A new project requires Spanish details and one cover among 1-5 images.");
        var validated = await InspectAsync(r.PendingImages, storage, ct);
        if (validated is null) return Bad("Staged project image is missing or invalid.");
        var committed = new List<string>();
        var succeeded = false;
        IResult result;
        try
        {
            using var mediaLease = await storage.AcquireMutationAsync(ct);
            await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
            {
                var name = r.Project.Translations.Single(t => t.LanguageCode == "es").Name;
                var p = new PublicProject
                {
                    Slug = await GenerateUniqueSlugAsync(name, db, ct),
                    CreatedAtUtc = DateTime.UtcNow, IsPublished = true
                };
                Assign(p, r.Project);
                db.Set<PublicProject>().Add(p);
                await db.SaveChangesAsync(ct);
                foreach (var (input, media) in validated)
                {
                    var key = await storage.CommitAsync(input.Token, ct);
                    committed.Add(key);
                    db.Set<PublicProjectImage>().Add(NewImage(p.Id, input, media, key));
                }
                SecurityAudit.Record(db, "PublicProjectPublished", "PublicProject", p.Id.ToString(), Actor(user));
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                result = Results.Created($"/api/admin/public-projects/{p.Id}",
                    new { p.Id, p.Slug, p.IsPublished, Version = EncodeVersion(p.RowVersion) });
                succeeded = true;
            }
        }
        catch (DbUpdateConcurrencyException) { result = Conflict("Project changed. Reload."); }
        catch (DbUpdateException) { result = Conflict("Project or featured position already exists."); }
        catch (IOException) { result = Results.Problem(title: "Media storage failed.", statusCode: 503); }
        finally
        {
            if (!succeeded) DeleteActiveQuietly(storage, committed);
        }
        if (succeeded) DeletePendingQuietly(storage, r.PendingImages.Select(i => i.Token));
        return result;
    }

    private static async Task<IResult> UpdateContent(int id, ContentWrite r, RevestikDbContext db,
        PublicCatalogMediaStorage storage, ClaimsPrincipal user, CancellationToken ct)
    {
        if (r?.Project is null || !ValidProjectWrite(r.Project) ||
            string.IsNullOrWhiteSpace(r.Project.Version) || !ValidStaged(r.PendingImages) ||
            !ValidExisting(r.ExistingImages) || r.RemovedImageIds is null ||
            r.RemovedImageIds.Distinct().Count() != r.RemovedImageIds.Count)
            return Bad("Invalid project, gallery or concurrency version.");
        var validated = await InspectAsync(r.PendingImages, storage, ct);
        if (validated is null) return Bad("Staged project image is missing or invalid.");
        var committed = new List<string>();
        var discarded = new List<string>();
        var succeeded = false;
        IResult result;
        try
        {
            using var mediaLease = await storage.AcquireMutationAsync(ct);
            await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
            {
                var p = await db.Set<PublicProject>().Include(p => p.Translations)
                    .Include(p => p.Spaces).SingleOrDefaultAsync(p => p.Id == id, ct);
                if (p is null) return Results.NotFound();
                if (!VersionMatches(r.Project.Version, p.RowVersion)) return Conflict("Project changed. Reload.");
                var current = await db.Set<PublicProjectImage>().Where(i => i.ProjectId == id).ToListAsync(ct);
                if (r.ExistingImages.Count != current.Count ||
                    r.ExistingImages.Any(x => !current.Any(i => i.Id == x.Id && VersionMatches(x.Version, i.RowVersion))) ||
                    r.RemovedImageIds.Any(x => !current.Any(i => i.Id == x)))
                    return Conflict("Gallery changed. Reload before saving.");

                var survivors = r.ExistingImages.Where(x => !r.RemovedImageIds.Contains(x.Id)).ToArray();
                if (survivors.Length + r.PendingImages.Count is < 1 or > 5 ||
                    survivors.Count(i => i.Role == "ProjectCover") +
                    r.PendingImages.Count(i => i.Role == "ProjectCover") != 1)
                    return Bad("A project requires one cover and at most five images.");

                // Clear an old cover before assigning a new one. SQL has a unique
                // filtered index for ProjectCover, so the order of statements matters.
                var oldCover = current.SingleOrDefault(i => i.Role == "ProjectCover");
                if (oldCover is not null &&
                    (r.RemovedImageIds.Contains(oldCover.Id) ||
                     survivors.All(i => i.Id != oldCover.Id || i.Role != "ProjectCover")))
                {
                    if (r.RemovedImageIds.Contains(oldCover.Id))
                    {
                        db.Set<PublicProjectImage>().Remove(oldCover);
                        discarded.Add(oldCover.StorageKey);
                    }
                    else oldCover.Role = "ProjectGallery";
                    await db.SaveChangesAsync(ct);
                }
                foreach (var image in current)
                {
                    if (image == oldCover && r.RemovedImageIds.Contains(image.Id)) continue;
                    if (r.RemovedImageIds.Contains(image.Id))
                    {
                        db.Set<PublicProjectImage>().Remove(image);
                        discarded.Add(image.StorageKey);
                        continue;
                    }
                    var change = survivors.Single(x => x.Id == image.Id);
                    image.Role = change.Role;
                    image.DisplayOrder = change.DisplayOrder;
                    image.AltEs = change.AltEs;
                    image.AltEn = change.AltEn;
                    image.AltDe = change.AltDe;
                }
                Assign(p, r.Project);
                foreach (var (input, media) in validated)
                {
                    var key = await storage.CommitAsync(input.Token, ct);
                    committed.Add(key);
                    db.Set<PublicProjectImage>().Add(NewImage(id, input, media, key));
                }
                SecurityAudit.Record(db, "PublicProjectUpdated", "PublicProject", id.ToString(), Actor(user));
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                result = Results.Ok(new { p.Id, p.Slug, p.IsPublished, Version = EncodeVersion(p.RowVersion) });
                succeeded = true;
            }
        }
        catch (DbUpdateConcurrencyException) { result = Conflict("Project or gallery changed. Reload."); }
        catch (DbUpdateException) { result = Conflict("Project or image conflicts with existing data."); }
        catch (IOException) { result = Results.Problem(title: "Media storage failed.", statusCode: 503); }
        finally
        {
            if (!succeeded) DeleteActiveQuietly(storage, committed);
        }
        if (succeeded)
        {
            DeletePendingQuietly(storage, r.PendingImages.Select(i => i.Token));
            DeleteActiveQuietly(storage, discarded);
        }
        return result;
    }

    private static async Task<IResult> Publish(int id, VersionWrite request, RevestikDbContext db,
        ClaimsPrincipal user, CancellationToken ct) =>
        await ChangePublication(id, request, db, user, true, ct);

    private static async Task<IResult> Unpublish(int id, VersionWrite request, RevestikDbContext db,
        ClaimsPrincipal user, CancellationToken ct) =>
        await ChangePublication(id, request, db, user, false, ct);

    private static async Task<IResult> ChangePublication(int id, VersionWrite request, RevestikDbContext db,
        ClaimsPrincipal user, bool publish, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var p = await db.Set<PublicProject>().SingleOrDefaultAsync(p => p.Id == id, ct);
        if (p is null) return Results.NotFound();
        if (!VersionMatches(request?.Version, p.RowVersion)) return Conflict("Project changed. Reload.");
        if (publish)
        {
            if (!PublicProjectClassification.IsKnownType(p.TypeCode) ||
                !await db.Set<PublicProjectTranslation>().AnyAsync(t => t.ProjectId == id &&
                    t.LanguageCode == "es" && t.Name != "" && t.Description != "", ct) ||
                await db.Set<PublicProjectImage>().CountAsync(i => i.ProjectId == id && i.Role == "ProjectCover", ct) != 1)
                return Bad("Spanish text, valid project type and cover image are required.");
        }
        p.IsPublished = publish;
        p.UpdatedAtUtc = DateTime.UtcNow;
        SecurityAudit.Record(db, publish ? "PublicProjectPublished" : "PublicProjectUnpublished",
            "PublicProject", id.ToString(), Actor(user));
        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(new { p.Id, p.IsPublished, Version = EncodeVersion(p.RowVersion) });
        }
        catch (DbUpdateConcurrencyException) { return Conflict("Project changed. Reload."); }
        catch (DbUpdateException) { return Conflict("Project conflicts with existing data."); }
    }

    private static async Task<IResult> Delete(int id, string version, RevestikDbContext db,
        PublicCatalogMediaStorage storage, ClaimsPrincipal user, CancellationToken ct)
    {
        List<string> keys;
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            var p = await db.Set<PublicProject>().SingleOrDefaultAsync(p => p.Id == id, ct);
            if (p is null) return Results.NotFound();
            if (!VersionMatches(version, p.RowVersion)) return Conflict("Project changed. Reload.");
            keys = await db.Set<PublicProjectImage>().Where(i => i.ProjectId == id)
                .Select(i => i.StorageKey).ToListAsync(ct);
            db.Set<PublicProject>().Remove(p);
            SecurityAudit.Record(db, "PublicProjectDeleted", "PublicProject", id.ToString(), Actor(user));
            try
            {
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateConcurrencyException) { return Conflict("Project changed. Reload."); }
            catch (DbUpdateException) { return Conflict("Project deletion failed due to a reference."); }
        }
        DeleteActiveQuietly(storage, keys);
        return Results.NoContent();
    }

    private static async Task<IResult> StageImage(HttpRequest request,
        PublicCatalogMediaStorage storage, CancellationToken ct)
    {
        if (!request.HasFormContentType ||
            request.ContentLength > PublicCatalogMediaStorage.MaxBytes + 65536)
            return Bad("Invalid multipart upload.");
        var form = await request.ReadFormAsync(ct);
        if (form.Files.Count != 1) return Bad("Exactly one image is required.");
        var file = form.Files[0];
        var role = form["role"].ToString();
        if (!ImageRole(role) || file.Length is <= 0 or > PublicCatalogMediaStorage.MaxBytes)
            return Bad("Invalid project image or size exceeds 5 MB.");
        try
        {
            await using var stream = file.OpenReadStream();
            var token = await storage.StageAsync(stream, file.FileName, "ProductCover", ct);
            var media = await storage.InspectPendingAsync(token, "ProductCover", ct);
            return Results.Ok(new { Token = token, media.ContentType,
                media.Width, media.Height, media.FrameCount });
        }
        catch (InvalidDataException) { return Bad("Invalid JPEG/PNG/WEBP photograph."); }
        catch (IOException) { return Results.Problem(title: "Media upload failed.", statusCode: 503); }
    }

    private static async Task<IResult> PreviewStagedImage(string token,
        PublicCatalogMediaStorage storage, CancellationToken ct)
    {
        try
        {
            var media = await storage.InspectPendingAsync(token, "ProductCover", ct);
            return Results.File(await storage.ReadPendingAsync(token, ct), media.ContentType);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or InvalidDataException)
        { return Results.NotFound(); }
    }

    private static IResult DeleteStagedImage(string token, PublicCatalogMediaStorage storage)
    {
        try { storage.DeletePending(token); return Results.NoContent(); }
        catch (InvalidDataException) { return Results.NotFound(); }
    }

    private static async Task<IResult> AdminImageContent(int id, RevestikDbContext db,
        PublicCatalogMediaStorage storage, CancellationToken ct)
    {
        var media = await db.Set<PublicProjectImage>().AsNoTracking().Where(i => i.Id == id)
            .Select(i => new { i.StorageKey, i.ContentType }).SingleOrDefaultAsync(ct);
        if (media is null) return Results.NotFound();
        try { return Results.File(await storage.ReadActiveAsync(media.StorageKey, ct), media.ContentType); }
        catch (FileNotFoundException) { return Results.NotFound(); }
        catch (DirectoryNotFoundException) { return Results.NotFound(); }
    }

    private static void DeleteActiveQuietly(PublicCatalogMediaStorage storage, IEnumerable<string> keys)
    {
        foreach (var key in keys)
            try { storage.DeleteActive(key); }
            catch (IOException) { /* orphan worker retries cleanup */ }
            catch (UnauthorizedAccessException) { /* orphan worker retries cleanup */ }
    }

    private static void DeletePendingQuietly(PublicCatalogMediaStorage storage, IEnumerable<string> keys)
    {
        foreach (var key in keys)
            try { storage.DeletePending(key); }
            catch (IOException) { /* orphan worker retries cleanup */ }
            catch (UnauthorizedAccessException) { /* orphan worker retries cleanup */ }
    }
}
