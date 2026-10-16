using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Projects;

namespace Revestik.Api.Services.Projects.Documents;

public sealed record ProjectDocumentSnapshot(int Id, int ProjectId, string DisplayName,
    string Category, int CurrentVersionId, int VersionNumber, long Version);

// B04 A+B persistence foundation. HTTP payload validation/format sniffing belongs to C+D.
public sealed class ProjectDocumentService(RevestikDbContext db, IProjectFileStorage storage)
{
    // Provisional safety ceiling until the B04 upload policy is implemented.
    private const long MaxBytes = 20L * 1024 * 1024;

    public async Task<ProjectDocumentSnapshot> CreateAsync(int projectId, string displayName,
        string category, string originalFileName, string contentType, Stream source,
        string actor, bool isAdmin, CancellationToken ct)
    {
        ValidateMetadata(displayName, category, originalFileName, contentType);
        await RequireEditorAsync(projectId, actor, isAdmin, ct);
        var content = await ReadBoundedAsync(source, ct);
        var checksum = Convert.ToHexString(SHA256.HashData(content));
        await using var upload = new MemoryStream(content, writable: false);
        var key = await storage.SaveAsync(upload, ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var document = new ProjectDocument
            {
                ProjectId = projectId, DisplayName = displayName.Trim(), Category = category.Trim(),
                CreatedByUserId = actor
            };
            db.Set<ProjectDocument>().Add(document);
            await db.SaveChangesAsync(ct);
            var version = MakeVersion(document.Id, 1, originalFileName, contentType, content.Length,
                checksum, key, actor);
            db.Set<ProjectDocumentVersion>().Add(version);
            await db.SaveChangesAsync(ct);
            document.CurrentVersionId = version.Id;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(document.Id, projectId, document.DisplayName, document.Category,
                version.Id, version.Number, document.Version);
        }
        catch
        {
            try { await storage.DeleteAsync(key, CancellationToken.None); }
            catch { /* Failed cleanup must be reported/handled by orphan reconciliation. */ }
            throw;
        }
    }

    public async Task<ProjectDocumentSnapshot> AddVersionAsync(int projectId, int documentId,
        long expectedDocumentVersion, string originalFileName, string contentType,
        Stream source, string actor, bool isAdmin, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(originalFileName) || originalFileName.Length > 250 ||
            string.IsNullOrWhiteSpace(contentType) || contentType.Length > 100)
            throw Invalid("Metadatos del archivo inválidos.");
        await RequireEditorAsync(projectId, actor, isAdmin, ct);
        var content = await ReadBoundedAsync(source, ct);
        var checksum = Convert.ToHexString(SHA256.HashData(content));
        await using var upload = new MemoryStream(content, writable: false);
        var key = await storage.SaveAsync(upload, ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var document = await db.Set<ProjectDocument>().SingleOrDefaultAsync(
                x => x.Id == documentId && x.ProjectId == projectId && x.DeletedAtUtc == null, ct);
            if (document is null) throw Invalid("Documento inexistente o eliminado.");
            if (document.Version != expectedDocumentVersion) throw Conflict();
            var last = await db.Set<ProjectDocumentVersion>()
                .Where(x => x.ProjectDocumentId == document.Id)
                .MaxAsync(x => (int?)x.Number, ct) ?? 0;
            var version = MakeVersion(document.Id, checked(last + 1), originalFileName,
                contentType, content.Length, checksum, key, actor);
            db.Set<ProjectDocumentVersion>().Add(version);
            await db.SaveChangesAsync(ct);
            document.CurrentVersionId = version.Id;
            document.Version++;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(document.Id, projectId, document.DisplayName, document.Category,
                version.Id, version.Number, document.Version);
        }
        catch
        {
            try { await storage.DeleteAsync(key, CancellationToken.None); }
            catch { /* Requires later orphan reconciliation. */ }
            throw;
        }
    }

    public async Task LinkEventAsync(int projectId, int documentId, int eventId,
        string actor, bool isAdmin, CancellationToken ct)
    {
        await RequireEditorAsync(projectId, actor, isAdmin, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var documentExists = await db.Set<ProjectDocument>().AnyAsync(
            x => x.Id == documentId && x.ProjectId == projectId && x.DeletedAtUtc == null, ct);
        var eventExists = await db.Set<ProjectEvent>().AnyAsync(
            x => x.Id == eventId && x.ProjectId == projectId && !x.IsVoided, ct);
        if (!documentExists || !eventExists) throw Invalid("Documento o evento inválido para este proyecto.");
        if (!await db.Set<ProjectDocumentEventLink>().AnyAsync(
            x => x.ProjectDocumentId == documentId && x.ProjectEventId == eventId, ct))
        {
            db.Set<ProjectDocumentEventLink>().Add(new ProjectDocumentEventLink
            {
                ProjectId = projectId, ProjectDocumentId = documentId, ProjectEventId = eventId, LinkedByUserId = actor
            });
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    public async Task SoftDeleteAsync(int projectId, int documentId, long expectedDocumentVersion,
        string actor, bool isAdmin, CancellationToken ct)
    {
        await RequireEditorAsync(projectId, actor, isAdmin, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var doc = await db.Set<ProjectDocument>().SingleOrDefaultAsync(
            x => x.Id == documentId && x.ProjectId == projectId && x.DeletedAtUtc == null, ct);
        if (doc is null) throw Invalid("Documento inexistente o eliminado.");
        if (doc.Version != expectedDocumentVersion) throw Conflict();
        doc.DeletedAtUtc = DateTime.UtcNow;
        doc.DeletedByUserId = actor;
        doc.Version++;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        // Physical deletion and cross-store reconciliation are deferred to B04 C+D/E.
    }

    public async Task UnlinkEventAsync(int projectId, int documentId, int eventId,
        string actor, bool isAdmin, CancellationToken ct)
    {
        await RequireEditorAsync(projectId, actor, isAdmin, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var documentExists = await db.Set<ProjectDocument>().AnyAsync(
            x => x.Id == documentId && x.ProjectId == projectId && x.DeletedAtUtc == null, ct);
        if (!documentExists) throw Invalid("Documento inexistente o eliminado.");
        var link = await db.Set<ProjectDocumentEventLink>().SingleOrDefaultAsync(
            x => x.ProjectDocumentId == documentId && x.ProjectEventId == eventId, ct);
        if (link is not null)
        {
            db.Set<ProjectDocumentEventLink>().Remove(link);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    private async Task RequireEditorAsync(int projectId, string actor, bool isAdmin, CancellationToken ct)
    {
        var project = await db.Set<Project>().AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == projectId, ct);
        if (project is null) throw Invalid("El proyecto no existe.");
        if (string.IsNullOrWhiteSpace(actor) || (!isAdmin && project.ResponsibleUserId != actor))
            throw new ProjectOperationException(ProjectFailureReason.Forbidden,
                "Solo el responsable o administrador puede modificar documentos.");
    }

    private static void ValidateMetadata(string name, string category, string filename, string mime)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 250 ||
            string.IsNullOrWhiteSpace(category) || category.Trim().Length > 40 ||
            string.IsNullOrWhiteSpace(filename) || filename.Length > 250 ||
            string.IsNullOrWhiteSpace(mime) || mime.Length > 100)
            throw Invalid("Metadatos del documento inválidos.");
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream source, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await source.ReadAsync(chunk, ct);
            if (read == 0) break;
            if (buffer.Length + read > MaxBytes) throw Invalid("El archivo supera el límite provisional de 20 MB.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        if (buffer.Length == 0) throw Invalid("El archivo está vacío.");
        return buffer.ToArray();
    }

    private static ProjectDocumentVersion MakeVersion(int documentId, int number,
        string filename, string mime, long size, string checksum, string key, string actor) => new()
    {
        ProjectDocumentId = documentId, Number = number, OriginalFileName = filename.Trim(),
        ContentType = mime.Trim(), SizeBytes = size, Sha256Hex = checksum,
        StorageKey = key, UploadedByUserId = actor, State = "Ready"
    };

    private static ProjectOperationException Invalid(string message) =>
        new(ProjectFailureReason.Invalid, message);
    private static ProjectOperationException Conflict() =>
        new(ProjectFailureReason.ConcurrencyConflict,
            "El documento cambió. Actualice la información antes de continuar.");
}
