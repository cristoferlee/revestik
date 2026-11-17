using Microsoft.Extensions.Caching.Memory;
using Revestik.Api.Services.Dashboard;

namespace Revestik.Api.Tests.Dashboard;

public sealed class DashboardFinancialCacheTests
{
    [Fact]
    public async Task RepeatedRequests_UseCachedResult()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new DashboardFinancialCache(memory);
        var calculations = 0;

        Task<int> Load(CancellationToken ct) =>
            Task.FromResult(++calculations);

        var first = await cache.GetOrLoadAsync(
            "gross-margin:2026:11", Load, false, CancellationToken.None);

        var second = await cache.GetOrLoadAsync(
            "gross-margin:2026:11", Load, false, CancellationToken.None);

        Assert.Equal(1, first);
        Assert.Equal(1, second);
        Assert.Equal(1, calculations);
    }

    [Fact]
    public async Task DifferentKeys_DoNotShareCachedResults()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new DashboardFinancialCache(memory);
        var calculations = 0;

        Task<int> Load(CancellationToken ct) =>
            Task.FromResult(++calculations);

        var november = await cache.GetOrLoadAsync(
            "gross-margin:2026:11", Load, false, CancellationToken.None);

        var december = await cache.GetOrLoadAsync(
            "gross-margin:2026:12", Load, false, CancellationToken.None);

        Assert.Equal(1, november);
        Assert.Equal(2, december);
        Assert.Equal(2, calculations);
    }

    [Fact]
    public async Task ConcurrentRequests_ExecuteCalculationOnce()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new DashboardFinancialCache(memory);
        var calculations = 0;

        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<int> Load(CancellationToken ct)
        {
            Interlocked.Increment(ref calculations);
            await release.Task.WaitAsync(ct);
            return 42;
        }

        var requests = Enumerable.Range(0, 12)
            .Select(_ => cache.GetOrLoadAsync(
                "gross-margin:2026:11",
                Load,
                false,
                CancellationToken.None))
            .ToArray();

        release.SetResult(true);

        var results = await Task.WhenAll(requests);

        Assert.All(results, result => Assert.Equal(42, result));
        Assert.Equal(1, calculations);
    }

    [Fact]
    public async Task ForceRefresh_ReplacesCachedResult()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new DashboardFinancialCache(memory);
        var calculations = 0;

        Task<int> Load(CancellationToken ct) =>
            Task.FromResult(++calculations);

        var first = await cache.GetOrLoadAsync(
            "gross-margin:2026:11", Load, false, CancellationToken.None);

        var cached = await cache.GetOrLoadAsync(
            "gross-margin:2026:11", Load, false, CancellationToken.None);

        var refreshed = await cache.GetOrLoadAsync(
            "gross-margin:2026:11", Load, true, CancellationToken.None);

        var afterRefresh = await cache.GetOrLoadAsync(
            "gross-margin:2026:11", Load, false, CancellationToken.None);

        Assert.Equal(1, first);
        Assert.Equal(1, cached);
        Assert.Equal(2, refreshed);
        Assert.Equal(2, afterRefresh);
        Assert.Equal(2, calculations);
    }

    [Fact]
    public async Task FailedCalculation_IsNotCached()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new DashboardFinancialCache(memory);
        var attempts = 0;

        Task<int> Load(CancellationToken ct)
        {
            attempts++;

            if (attempts == 1)
                return Task.FromException<int>(
                    new InvalidOperationException("Simulated SQL failure"));

            return Task.FromResult(99);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => cache.GetOrLoadAsync(
                "gross-margin:2026:11",
                Load,
                false,
                CancellationToken.None));

        var result = await cache.GetOrLoadAsync(
            "gross-margin:2026:11",
            Load,
            false,
            CancellationToken.None);

        Assert.Equal(99, result);
        Assert.Equal(2, attempts);
    }
}