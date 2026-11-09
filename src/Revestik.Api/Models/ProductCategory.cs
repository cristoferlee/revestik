namespace Revestik.Api.Models;

public sealed class ProductCategory
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }


    // Public presentation metadata; category identity remains shared with inventory.
    public string? PublicSlug { get; set; }
    public string? PublicNameEn { get; set; }
    public string? PublicNameDe { get; set; }
    public string? PublicDescriptionEs { get; set; }
    public string? PublicDescriptionEn { get; set; }
    public string? PublicDescriptionDe { get; set; }
    public string? PublicHeroTitleEs { get; set; }
    public string? PublicHeroTitleEn { get; set; }
    public string? PublicHeroTitleDe { get; set; }
    public string? PublicHeroDescriptionEs { get; set; }
    public string? PublicHeroDescriptionEn { get; set; }
    public string? PublicHeroDescriptionDe { get; set; }
    public int PublicDisplayOrder { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<Product> Products { get; set; } = [];

    public ICollection<ProductSubcategory> Subcategories { get; set; } = [];
}