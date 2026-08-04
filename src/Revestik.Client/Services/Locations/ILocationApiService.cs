using Revestik.Shared.Locations;

namespace Revestik.Client.Services.Locations;

public interface ILocationApiService
{
    Task<IReadOnlyList<LocationOptionResponse>> GetProvincesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LocationOptionResponse>> GetCantonsAsync(
        string provinceCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LocationOptionResponse>> GetDistrictsAsync(
        string provinceCode,
        string cantonCode,
        CancellationToken cancellationToken = default);
}