using Revestik.Shared.Locations;

namespace Revestik.Api.Integrations.Locations;

public enum LocationCatalogStatus
{
    Available,
    NotFound,
    Unavailable
}

public sealed record LocationCatalogResult(
    LocationCatalogStatus Status,
    IReadOnlyList<LocationOptionResponse> Locations);