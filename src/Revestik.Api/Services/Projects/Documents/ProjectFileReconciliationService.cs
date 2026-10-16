using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;

namespace Revestik.Api.Services.Projects.Documents;

public sealed record ProjectFileReconciliationResult(int Examined, int Unreferenced,
    int Deleted, int Failed, bool DryRun);

// Run only as an explicit maintenance operation after upload activity is quiescent.
// This is NOT a transactional guarantee between SQL Server and the filesystem.
public sealed class ProjectFileReconciliationService(
    RevestikDbContext db, IProjectFileStorage storage, IProjectFileInventory inventory)
{
    private static readonly TimeSpan MinimumAge = TimeSpan.FromHours(24);

    public async Task<ProjectFileReconciliationResult> ReconcileAsync(bool dryRun,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.Subtract(MinimumAge);
        var candidates = await inventory.ListKeysOlderThanAsync(cutoff, cancellationToken);
        var referenced = await db.Set<ProjectDocumentVersion>().AsNoTracking()
            .Select(v => new { v.StorageKey, v.ThumbnailStorageKey })
            .ToListAsync(cancellationToken);
        var liveKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in referenced)
        {
            liveKeys.Add(row.StorageKey);
            if (!string.IsNullOrEmpty(row.ThumbnailStorageKey)) liveKeys.Add(row.ThumbnailStorageKey);
        }

        var unreferenced = 0;
        var deleted = 0;
        var failed = 0;
        foreach (var key in candidates.Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (liveKeys.Contains(key)) continue;
            unreferenced++;
            if (dryRun) continue;
            // Double-check immediately before deletion; execute with writers paused.
            var newlyReferenced = await db.Set<ProjectDocumentVersion>().AsNoTracking()
                .AnyAsync(v => v.StorageKey == key || v.ThumbnailStorageKey == key,
                    cancellationToken);
            if (newlyReferenced) continue;
            try
            {
                await storage.DeleteAsync(key, cancellationToken);
                deleted++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (IOException)
            {
                failed++;
            }
            catch (UnauthorizedAccessException)
            {
                failed++;
            }
        }
        return new(candidates.Count, unreferenced, deleted, failed, dryRun);
    }
}
