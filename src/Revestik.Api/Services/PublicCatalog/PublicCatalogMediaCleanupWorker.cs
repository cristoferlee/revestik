using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models.PublicCatalog;

namespace Revestik.Api.Services.PublicCatalog;

public sealed class PublicCatalogMediaCleanupWorker(
    PublicCatalogMediaStorage storage,
    IServiceScopeFactory scopeFactory,
    ILogger<PublicCatalogMediaCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deleted = storage.PrunePending(TimeSpan.FromHours(24));
                if (deleted != 0) logger.LogInformation("Pruned {Count} stale public-catalog uploads.", deleted);
                // Never delete durable files unless a successful database query
                // confirms they are unreferenced. 24h grace handles in-flight writes.
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<RevestikDbContext>();
                var keys = await db.Set<PublicCatalogImage>().AsNoTracking()
                    .Select(i => i.StorageKey).ToListAsync(stoppingToken);
                // Home Hero images share the existing file store. Never delete a referenced Home image.
                keys.AddRange(await db.PublicHomeHeroSlides.AsNoTracking()
                    .Where(x => x.ImageStorageKey != null)
                    .Select(x => x.ImageStorageKey!).ToListAsync(stoppingToken));
                var homeSectionKeys = await db.PublicHomeSections.AsNoTracking()
                    .Where(x => x.ImageStorageKey != null).Select(x => x.ImageStorageKey!).ToListAsync(stoppingToken);
                keys.AddRange(homeSectionKeys);
                var orphans = storage.PruneActiveOrphans(keys.ToHashSet(StringComparer.Ordinal), TimeSpan.FromHours(24));
                if (orphans != 0) logger.LogInformation("Pruned {Count} unreferenced public-catalog files.", orphans);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not prune pending public-catalog uploads.");
            }
            try { if (!await timer.WaitForNextTickAsync(stoppingToken)) break; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
