using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Projects;

namespace Revestik.Api.Services.Projects.Documents;

// Queries never reveal private storage keys. The storage layer is accessed only after an
// authenticated ManageProjects request and a Project/Document ownership check.
public sealed class ProjectDocumentQueryService(RevestikDbContext db, IProjectFileStorage storage)
{
    public async Task<bool> ProjectExistsAsync(int projectId, CancellationToken ct) =>
        await db.Set<Project>().AsNoTracking().AnyAsync(x => x.Id == projectId, ct);

    public async Task<IReadOnlyList<ProjectDocumentListItem>?> ListAsync(int projectId, CancellationToken ct)
    {
        if (!await ProjectExistsAsync(projectId, ct)) return null;
        return await db.Set<ProjectDocument>().AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.DeletedAtUtc == null)
            .OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Select(x => new ProjectDocumentListItem(x.Id, x.ProjectId, x.DisplayName,
                x.Category, x.CurrentVersionId, x.Version, x.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<ProjectDocumentDetails?> GetAsync(int projectId, int documentId, CancellationToken ct)
    {
        var document = await db.Set<ProjectDocument>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == documentId && x.DeletedAtUtc == null, ct);
        if (document is null) return null;
        var versions = await db.Set<ProjectDocumentVersion>().AsNoTracking()
            .Where(x => x.ProjectDocumentId == documentId && x.State == "Ready")
            .OrderByDescending(x => x.Number)
            .Select(x => new ProjectDocumentVersionItem(x.Id, x.Number, x.OriginalFileName,
                x.ContentType, x.SizeBytes, x.Sha256Hex, x.UploadedAtUtc,
                x.UploadedByUserId, x.Id == document.CurrentVersionId))
            .ToListAsync(ct);
        return new(new(document.Id, projectId, document.DisplayName, document.Category,
            document.CurrentVersionId, document.Version, document.CreatedAtUtc), versions);
    }

    public async Task<ProjectDocumentDownload?> OpenAsync(int projectId, int documentId,
        int? versionId, CancellationToken ct)
    {
        var document = await db.Set<ProjectDocument>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == documentId && x.ProjectId == projectId && x.DeletedAtUtc == null, ct);
        if (document is null) return null;
        var selectedId = versionId ?? document.CurrentVersionId;
        if (!selectedId.HasValue) return null;
        var version = await db.Set<ProjectDocumentVersion>().AsNoTracking().SingleOrDefaultAsync(
            x => x.ProjectDocumentId == documentId && x.Id == selectedId.Value && x.State == "Ready", ct);
        if (version is null) return null;
        var stream = await storage.OpenReadAsync(version.StorageKey, ct);
        return new(stream, version.OriginalFileName, version.ContentType);
    }
}

public sealed record ProjectDocumentDownload(Stream Content, string FileName, string ContentType);
