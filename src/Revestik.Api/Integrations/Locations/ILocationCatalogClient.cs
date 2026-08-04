namespace Revestik.Api.Integrations.Locations;

public interface ILocationCatalogClient
{
    Task<LocationCatalogResult> GetProvincesAsync(
        CancellationToken cancellationToken = default);

    Task<LocationCatalogResult> GetCantonsAsync(
        string provinceCode,
        CancellationToken cancellationToken = default);

    Task<LocationCatalogResult> GetDistrictsAsync(
        string provinceCode,
        string cantonCode,
        CancellationToken cancellationToken = default);
}