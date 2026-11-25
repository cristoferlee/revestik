using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Models.PublicProjects;

namespace Revestik.Api.Services.PublicCatalog;

public sealed class PublicCatalogMediaCleanupWorker(
    PublicCatalogMediaStorage storage,
    IServiceScopeFactory scopeFactory,
    ILogger<PublicCatalogMediaCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval =
        TimeSpan.FromHours(6);

    private static readonly TimeSpan ActiveCleanupInterval =
        TimeSpan.FromDays(30);

    private static readonly TimeSpan GracePeriod =
        TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunMaintenanceCycleAsync(
                    DateTimeOffset.UtcNow, stoppingToken);

                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task RunMaintenanceCycleAsync(
        DateTimeOffset now,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Temporary uploads do not require database access.
        try
        {
            var pending = storage.PrunePending(GracePeriod);

            if (pending != 0)
                logger.LogInformation(
                    "Pruned {Count} stale public-catalog uploads.",
                    pending);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "Could not prune pending public-catalog uploads.");
        }

        FileStream lease;

        try
        {
            lease = storage.AcquireCleanupLease();
        }
        catch (IOException ex)
        {
            logger.LogWarning(
                ex, "Public media cleanup lease unavailable.");
            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(
                ex, "Public media cleanup storage unavailable.");
            return;
        }

        using (lease)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                now = now.ToUniversalTime();
                var next = storage.ReadNextCleanupUtc();

                // Initialize without touching SQL.
                if (next is null)
                {
                    storage.WriteNextCleanupUtc(
                        now.Add(ActiveCleanupInterval));
                    return;
                }

                // Detect a schedule inconsistent with the expected interval.
                // Do not delete durable files when the schedule is invalid.
                if (next.Value > now.Add(ActiveCleanupInterval))
                    throw new InvalidDataException(
                        "Public media cleanup schedule is unexpectedly far in the future.");

                if (now < next.Value)
                    return;

                // No database scope is created until maintenance is due.
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider
                    .GetRequiredService<RevestikDbContext>();

                var keys = await db.Set<PublicCatalogImage>()
                    .AsNoTracking()
                    .Select(i => i.StorageKey)
                    .ToListAsync(ct);

                // Slots 1-3: Home. Slots 4-6: Finishes.
                keys.AddRange(await db.PublicHomeHeroSlides
                    .AsNoTracking()
                    .Where(x => x.ImageStorageKey != null)
                    .Select(x => x.ImageStorageKey!)
                    .ToListAsync(ct));

                keys.AddRange(await db.PublicHomeSections
                    .AsNoTracking()
                    .Where(x => x.ImageStorageKey != null)
                    .Select(x => x.ImageStorageKey!)
                    .ToListAsync(ct));

                keys.AddRange(await db.PublicSolutionsSections
                    .AsNoTracking()
                    .Where(x => x.Key == "hero" &&
                        x.ImageStorageKey != null)
                    .Select(x => x.ImageStorageKey!)
                    .ToListAsync(ct));

                keys.AddRange(await db.Set<PublicProjectImage>()
                    .AsNoTracking()
                    .Select(x => x.StorageKey)
                    .ToListAsync(ct));

                ct.ThrowIfCancellationRequested();

                // All reference queries must succeed before deletion.
                var orphans = storage.PruneActiveOrphans(
                    keys.ToHashSet(StringComparer.Ordinal),
                    GracePeriod);

                storage.WriteNextCleanupUtc(
                    now.Add(ActiveCleanupInterval));

                logger.LogInformation(
                    "Monthly public media cleanup completed. " +
                    "Removed {Count} unreferenced files.",
                    orphans);
            }
            catch (OperationCanceledException)
                when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Preserve the existing schedule and retry at the next
                // check. A failed SQL read must never trigger deletion.
                logger.LogError(
                    ex, "Could not complete monthly public media cleanup.");
            }
        }
    }
}
