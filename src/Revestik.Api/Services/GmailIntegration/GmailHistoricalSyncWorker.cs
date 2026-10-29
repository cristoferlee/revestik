using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.GmailIntegration;

// Work is hosted by the API, not tied to the initiating HTTP request.
// One active job per API process. Multi-replica deployments require a distributed lease.
internal sealed class GmailHistoricalSyncWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<GmailHistoricalSyncWorker> logger) : BackgroundService
{
    private readonly SemaphoreSlim signal = new(0, 1);
    private readonly object statusGate = new();
    private bool queued;
    private bool running;
    private int pages;
    private int scanned;
    private int staged;
    private int duplicates;
    private int failed;
    private string? error;
    private DateTime? startedUtc;
    private DateTime? finishedUtc;

    public bool TryStart()
    {
        lock (statusGate)
        {
            if (queued || running) return false;
            queued = true;
            pages = scanned = staged = duplicates = failed = 0;
            error = null;
            startedUtc = DateTime.UtcNow;
            finishedUtc = null;
            signal.Release();
            return true;
        }
    }

    public GmailHistoricalSyncProgress Snapshot()
    {
        lock (statusGate)
            return new GmailHistoricalSyncProgress(
                queued, running, pages, scanned, staged, duplicates, failed,
                error, startedUtc, finishedUtc);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await signal.WaitAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }

            lock (statusGate) { queued = false; running = true; }
            try
            {
                // Each SyncAsync persists its current Gmail page and attachment ledger.
                // The delay also limits per-user Gmail API traffic from long imports.
                var firstPage = true;
                while (!stoppingToken.IsCancellationRequested)
                {
                    using var scope = scopeFactory.CreateScope();
                    var stateStore = scope.ServiceProvider.GetRequiredService<IGmailIntegrationStateStore>();
                    var before = await stateStore.LoadAsync(stoppingToken);
                    if (!firstPage && before.HistoricalSweepCompleted && string.IsNullOrWhiteSpace(before.SweepQuery))
                        break;
                    firstPage = false;

                    var service = scope.ServiceProvider.GetRequiredService<IGmailIntegrationService>();
                    GmailSyncResultResponse result = await service.SyncAsync(stoppingToken);
                    lock (statusGate)
                    {
                        pages++;
                        scanned += result.MessagesScanned;
                        staged += result.Staged;
                        duplicates += result.Duplicates;
                        failed += result.Failed;
                    }
                    if (result.Failed > 0)
                    {
                        lock (statusGate) error = "Hay mensajes pendientes de reintento; reanudá la importación.";
                        break;
                    }
                    // Bound each background job; a new start resumes at the stored checkpoint.
                    if (pages >= 50)
                    {
                        lock (statusGate) error = "Se alcanzó el límite de seguridad de 50 lotes. Podés reanudar.";
                        break;
                    }
                    var after = await stateStore.LoadAsync(stoppingToken);
                    if (after.HistoricalSweepCompleted && string.IsNullOrWhiteSpace(after.SweepQuery))
                        break;

                    await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Gmail historical background synchronization stopped; checkpoint retained.");
                lock (statusGate) error = ex is GmailIntegrationException
                    ? ex.Message
                    : "La importación se interrumpió. El progreso quedó guardado; podés reanudarla.";
            }
            finally
            {
                lock (statusGate) { running = false; finishedUtc = DateTime.UtcNow; }
            }
        }
    }
}

internal sealed record GmailHistoricalSyncProgress(
    bool Queued, bool Running, int Pages, int MessagesScanned,
    int Staged, int Duplicates, int Failed, string? Error,
    DateTime? StartedUtc, DateTime? FinishedUtc);
