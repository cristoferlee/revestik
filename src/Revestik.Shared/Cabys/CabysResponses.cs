namespace Revestik.Shared.Cabys;

public sealed record CabysCatalogStatusResponse(
    bool IsLoaded,
    string? CurrentVersion,
    int ItemCount,
    DateTime? ImportedAtUtc,
    string? SourceFileName,
    string? SourceSha256);

public sealed record CabysCatalogLoadResponse(
    string Version,
    int ItemCount,
    bool WasAlreadyLoaded);

public sealed record CabysListItemResponse(
    string Code,
    string Description,
    string TaxReference,
    string Category1Description,
    string Category4Description,
    string Category8Description,
    string CatalogVersion);

public sealed record CabysDetailResponse(
    string Code,
    string Description,
    string TaxReference,
    string Category1Code,
    string Category1Description,
    string Category2Code,
    string Category2Description,
    string Category3Code,
    string Category3Description,
    string Category4Code,
    string Category4Description,
    string Category5Code,
    string Category5Description,
    string Category6Code,
    string Category6Description,
    string Category7Code,
    string Category7Description,
    string Category8Code,
    string Category8Description,
    string Includes,
    string Excludes,
    string CatalogVersion);
