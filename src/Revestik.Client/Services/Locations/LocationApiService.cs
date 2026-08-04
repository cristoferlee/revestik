using System.Net.Http.Json;
using Revestik.Shared.Locations;

namespace Revestik.Client.Services.Locations;

public sealed class LocationApiService(HttpClient httpClient)
    : ILocationApiService
{
    public Task<IReadOnlyList<LocationOptionResponse>>
        GetProvincesAsync(
            CancellationToken cancellationToken = default)
    {
        return GetLocationsAsync(
            "api/locations/provinces",
            cancellationToken);
    }

    public Task<IReadOnlyList<LocationOptionResponse>>
        GetCantonsAsync(
            string provinceCode,
            CancellationToken cancellationToken = default)
    {
        return GetLocationsAsync(
            $"api/locations/provinces/{provinceCode}/cantons",
            cancellationToken);
    }

    public Task<IReadOnlyList<LocationOptionResponse>>
        GetDistrictsAsync(
            string provinceCode,
            string cantonCode,
            CancellationToken cancellationToken = default)
    {
        return GetLocationsAsync(
            $"api/locations/provinces/{provinceCode}/cantons/{cantonCode}/districts",
            cancellationToken);
    }

    private async Task<IReadOnlyList<LocationOptionResponse>>
        GetLocationsAsync(
            string requestUri,
            CancellationToken cancellationToken)
    {
        return await httpClient
            .GetFromJsonAsync<List<LocationOptionResponse>>(
                requestUri,
                cancellationToken) ?? [];
    }
}