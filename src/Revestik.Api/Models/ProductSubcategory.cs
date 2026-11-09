namespace Revestik.Api.Models;

/// <summary>
/// Shared classification vocabulary. Products remain separate in the
/// operational inventory and the public storefront.
/// </summary>
public sealed class ProductSubcategory
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public ProductCategory Category { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Public presentation metadata; no parallel public taxonomy.
    public string? PublicSlug { get; set; }
    public string? PublicNameEn { get; set; }
    public string? PublicNameDe { get; set; }
    public string? PublicDescriptionEs { get; set; }
    public string? PublicDescriptionEn { get; set; }
    public string? PublicDescriptionDe { get; set; }
    public int PublicDisplayOrder { get; set; }

    public ICollection<Product> Products { get; set; } = [];
}
