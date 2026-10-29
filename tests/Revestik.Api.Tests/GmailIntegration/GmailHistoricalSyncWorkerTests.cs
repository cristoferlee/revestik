using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Revestik.Api.Services.GmailIntegration;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.GmailIntegration;

public sealed class GmailHistoricalSyncWorkerTests
{
    [Fact]
    public void TryStart_DoesNotQueueTheSameJobTwice()
    {
        var state = new FakeStateStore();
        var service = new FakeGmailService((_, _) => Task.FromResult(Result()));
        using var provider = BuildProvider(state, service);
        using var worker = CreateWorker(provider);

        Assert.True(worker.TryStart());
        Assert.False(worker.TryStart());
        Assert.True(worker.Snapshot().Queued);
        Assert.False(worker.Snapshot().Running);
    }

    [Fact]
    public async Task CompletedSweep_StopsAfterOnePage_AndAllowsAnotherStart()
    {
        var state = new FakeStateStore();
        var service = new FakeGmailService((_, _) =>
        {
            state.Value.HistoricalSweepCompleted = true;
            state.Value.SweepQuery = null;
            return Task.FromResult(Result(scanned: 5, staged: 3));
        });
        using var provider = BuildProvider(state, service);
        using var worker = CreateWorker(provider);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(worker.TryStart());
            await UntilAsync(() => worker.Snapshot().FinishedUtc.HasValue);
            var progress = worker.Snapshot();
            Assert.False(progress.Running);
            Assert.False(progress.Queued);
            Assert.Equal(1, progress.Pages);
            Assert.Equal(5, progress.MessagesScanned);
            Assert.Equal(3, progress.Staged);
            Assert.Equal(1, service.Calls);
            Assert.True(worker.TryStart());
            await UntilAsync(() => worker.Snapshot().FinishedUtc.HasValue && !worker.Snapshot().Running && service.Calls == 2);
        }
        finally { await worker.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task FailedPage_StopsAndCanResumeUsingPersistedState()
    {
        var state = new FakeStateStore();
        state.Value.SweepQuery = "has:attachment filename:xml before:2000000000";
        state.Value.SweepPageToken = "checkpoint-page-2";
        var calls = 0;
        var service = new FakeGmailService((_, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
                return Task.FromResult(Result(failed: 1));
            state.Value.HistoricalSweepCompleted = true;
            state.Value.SweepQuery = null;
            state.Value.SweepPageToken = null;
            return Task.FromResult(Result(scanned: 2, staged: 1));
        });
        using var provider = BuildProvider(state, service);
        using var worker = CreateWorker(provider);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(worker.TryStart());
            await UntilAsync(() => worker.Snapshot().FinishedUtc.HasValue);
            Assert.Equal(1, worker.Snapshot().Failed);
            Assert.NotNull(worker.Snapshot().Error);
            Assert.Equal("checkpoint-page-2", state.Value.SweepPageToken);

            Assert.True(worker.TryStart());
            await UntilAsync(() => service.Calls == 2 && worker.Snapshot().FinishedUtc.HasValue && !worker.Snapshot().Running);
            Assert.Equal(1, worker.Snapshot().Staged);
            Assert.Equal(0, worker.Snapshot().Failed);
            Assert.True(state.Value.HistoricalSweepCompleted);
        }
        finally { await worker.StopAsync(CancellationToken.None); }
    }

    private static GmailHistoricalSyncWorker CreateWorker(ServiceProvider provider) =>
        new(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<GmailHistoricalSyncWorker>.Instance);

    private static ServiceProvider BuildProvider(FakeStateStore state, FakeGmailService service) =>
        new ServiceCollection()
            .AddSingleton<IGmailIntegrationStateStore>(state)
            .AddScoped<IGmailIntegrationService>(_ => service)
            .BuildServiceProvider();

    private static GmailSyncResultResponse Result(int scanned = 0, int staged = 0, int failed = 0) =>
        new(scanned, staged, staged, 0, 0, 0, failed, 0, DateTime.UtcNow);

    private static async Task UntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class FakeStateStore : IGmailIntegrationStateStore
    {
        public GmailIntegrationState Value { get; } = new();
        public Task<GmailIntegrationState> LoadAsync(CancellationToken ct) => Task.FromResult(Value);
        public Task SaveAsync(GmailIntegrationState state, CancellationToken ct) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeGmailService(Func<int, CancellationToken, Task<GmailSyncResultResponse>> sync) : IGmailIntegrationService
    {
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        public Task<GmailSyncResultResponse> SyncAsync(CancellationToken cancellationToken) =>
            sync(Interlocked.Increment(ref calls), cancellationToken);
        public Task<GmailIntegrationStatusResponse> GetStatusAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public string CreateAuthorizationUrl(string userId) => throw new NotSupportedException();
        public Task CompleteAuthorizationAsync(string code, string state, string userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task DisconnectAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
