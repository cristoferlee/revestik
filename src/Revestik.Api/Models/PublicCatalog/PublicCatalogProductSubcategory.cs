namespace Revestik.Api.Models.PublicCatalog;

public sealed class PublicCatalogProductSubcategory
{
    public int ProductId { get; set; }
    public PublicCatalogProduct Product { get; set; } = null!;
    public int SubcategoryId { get; set; }
    public PublicCatalogSubcategory Subcategory { get; set; } = null!;
}
