namespace Revestik.Api.Models.PublicCatalog;

public sealed class PublicCatalogSubcategory
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public PublicCatalogCategory Category { get; set; } = null!;
    public string Slug { get; set; } = string.Empty;
    public string NameEs { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? NameDe { get; set; }
    public string? DescriptionEs { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionDe { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<PublicCatalogProductSubcategory> ProductLinks { get; set; } = new List<PublicCatalogProductSubcategory>();
}
