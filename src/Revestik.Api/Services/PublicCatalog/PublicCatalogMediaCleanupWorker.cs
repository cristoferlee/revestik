namespace Revestik.Api.Services.PublicCatalog;

public sealed class PublicCatalogMediaCleanupWorker(
    PublicCatalogMediaStorage storage,
    ILogger<PublicCatalogMediaCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        do
        {
            await RunMaintenanceCycleAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task RunMaintenanceCycleAsync(CancellationToken ct)
    {
        try
        {
            using var lease = await storage.AcquireMutationAsync(ct);
            var count = storage.PrunePending(TimeSpan.FromHours(24));
            if (count > 0)
                logger.LogInformation("Pruned {Count} stale public media uploads.", count);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not prune pending public media uploads.");
        }
    }
}
