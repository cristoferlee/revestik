namespace Revestik.Api.Models.PublicCatalog;

public sealed class PublicCatalogCategory
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string NameEs { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? NameDe { get; set; }
    public string? DescriptionEs { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionDe { get; set; }
    public string? HeroTitleEs { get; set; }
    public string? HeroTitleEn { get; set; }
    public string? HeroTitleDe { get; set; }
    public string? HeroDescriptionEs { get; set; }
    public string? HeroDescriptionEn { get; set; }
    public string? HeroDescriptionDe { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<PublicCatalogSubcategory> Subcategories { get; set; } = new List<PublicCatalogSubcategory>();
}
