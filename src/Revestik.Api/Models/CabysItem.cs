namespace Revestik.Api.Models;

public sealed class CabysItem
{
    public string CatalogVersion { get; set; } = string.Empty;
    public CabysCatalogVersion Catalog { get; set; } = null!;

    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TaxReference { get; set; } = string.Empty;

    public string Category1Code { get; set; } = string.Empty;
    public string Category1Description { get; set; } = string.Empty;
    public string Category2Code { get; set; } = string.Empty;
    public string Category2Description { get; set; } = string.Empty;
    public string Category3Code { get; set; } = string.Empty;
    public string Category3Description { get; set; } = string.Empty;
    public string Category4Code { get; set; } = string.Empty;
    public string Category4Description { get; set; } = string.Empty;
    public string Category5Code { get; set; } = string.Empty;
    public string Category5Description { get; set; } = string.Empty;
    public string Category6Code { get; set; } = string.Empty;
    public string Category6Description { get; set; } = string.Empty;
    public string Category7Code { get; set; } = string.Empty;
    public string Category7Description { get; set; } = string.Empty;
    public string Category8Code { get; set; } = string.Empty;
    public string Category8Description { get; set; } = string.Empty;

    public string Includes { get; set; } = string.Empty;
    public string Excludes { get; set; } = string.Empty;
}
