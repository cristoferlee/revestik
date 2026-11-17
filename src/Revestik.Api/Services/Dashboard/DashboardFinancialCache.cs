using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace Revestik.Api.Services.Dashboard;

/// <summary>
/// Short-lived, process-local cache for shared dashboard financial summaries.
/// Does not cache user-specific responses or authorization decisions.
/// </summary>
public sealed class DashboardFinancialCache(IMemoryCache cache)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, SemaphoreSlim> gates =
        new(StringComparer.Ordinal);

    private sealed record CacheEntry<T>(T Value, DateTimeOffset CreatedAtUtc);

    public async Task<T> GetOrLoadAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> loader,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var requestedAtUtc = DateTimeOffset.UtcNow;

        if (!forceRefresh &&
            cache.TryGetValue(key, out CacheEntry<T>? hit) &&
            hit is not null)
        {
            return hit.Value;
        }

        var gate = gates.GetOrAdd(
            key,
            static _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken);

        try
        {
            if (cache.TryGetValue(key, out CacheEntry<T>? current) &&
                current is not null &&
                (!forceRefresh ||
                 current.CreatedAtUtc >= requestedAtUtc))
            {
                return current.Value;
            }

            var value = await loader(cancellationToken);

            cache.Set(
                key,
                new CacheEntry<T>(value, DateTimeOffset.UtcNow),
                Lifetime);

            return value;
        }
        finally
        {
            gate.Release();
        }
    }
}
