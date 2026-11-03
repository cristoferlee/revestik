namespace Revestik.Api.Models.PublicCatalog;

public sealed class PublicCatalogProduct
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public string? SalesUnit { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<PublicCatalogProductSubcategory> SubcategoryLinks { get; set; } = new List<PublicCatalogProductSubcategory>();
    public ICollection<PublicCatalogProductTranslation> Translations { get; set; } = new List<PublicCatalogProductTranslation>();
}
