using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Revestik.Shared.Locations;

namespace Revestik.Api.Integrations.Locations;

public sealed class LocationCatalogClient(
    HttpClient httpClient,
    IMemoryCache memoryCache,
    ILogger<LocationCatalogClient> logger)
    : ILocationCatalogClient
{
    private static readonly TimeSpan CacheDuration =
        TimeSpan.FromHours(24);

    public Task<LocationCatalogResult> GetProvincesAsync(
        CancellationToken cancellationToken = default)
    {
        return GetLocationsAsync(
            cacheKey: "locations:provinces",
            requestUri: "provincias.json",
            codeLength: 1,
            cancellationToken);
    }

    public Task<LocationCatalogResult> GetCantonsAsync(
        string provinceCode,
        CancellationToken cancellationToken = default)
    {
        return GetLocationsAsync(
            cacheKey: $"locations:province:{provinceCode}:cantons",
            requestUri:
                $"provincia/{provinceCode}/cantones.json",
            codeLength: 2,
            cancellationToken);
    }

    public Task<LocationCatalogResult> GetDistrictsAsync(
    string provinceCode,
    string cantonCode,
    CancellationToken cancellationToken = default)
    {
        var externalCantonCode = int.Parse(cantonCode).ToString();

        return GetLocationsAsync(
            cacheKey:
                $"locations:province:{provinceCode}:canton:{cantonCode}:districts",
            requestUri:
                $"provincia/{provinceCode}/canton/{externalCantonCode}/distritos.json",
            codeLength: 2,
            cancellationToken);
    }

    private async Task<LocationCatalogResult> GetLocationsAsync(
        string cacheKey,
        string requestUri,
        int codeLength,
        CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue(
                cacheKey,
                out IReadOnlyList<LocationOptionResponse>? cachedLocations) &&
            cachedLocations is not null)
        {
            return new LocationCatalogResult(
                LocationCatalogStatus.Available,
                cachedLocations);
        }

        try
        {
            using var response = await httpClient.GetAsync(
                requestUri,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new LocationCatalogResult(
                    LocationCatalogStatus.NotFound,
                    []);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Location catalog returned status code {StatusCode}.",
                    response.StatusCode);

                return new LocationCatalogResult(
                    LocationCatalogStatus.Unavailable,
                    []);
            }

            var locations = await response.Content
                .ReadFromJsonAsync<Dictionary<string, string>>(
                    cancellationToken);

            if (locations is null || locations.Count == 0)
            {
                return new LocationCatalogResult(
                    LocationCatalogStatus.NotFound,
                    []);
            }

            var normalizedLocations = locations
                .Select(location => new LocationOptionResponse(
                    location.Key.PadLeft(codeLength, '0'),
                    location.Value))
                .OrderBy(location => location.Code)
                .ToArray();

            memoryCache.Set(
                cacheKey,
                normalizedLocations,
                CacheDuration);

            return new LocationCatalogResult(
                LocationCatalogStatus.Available,
                normalizedLocations);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Location catalog request timed out.");

            return new LocationCatalogResult(
                LocationCatalogStatus.Unavailable,
                []);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Location catalog request failed.");

            return new LocationCatalogResult(
                LocationCatalogStatus.Unavailable,
                []);
        }
    }
}