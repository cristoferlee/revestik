namespace Revestik.Api.Models.PublicCatalog;

/// <summary>
/// Metadata only. The original bytes live in catalog-specific persistent storage,
/// never in SQL Server or the operational product inventory.
/// </summary>
public sealed class PublicCatalogImage
{
    public int Id { get; set; }
    public int? ProductId { get; set; }
    public int? CategoryId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public long FileSizeBytes { get; set; }
    public int DisplayOrder { get; set; }
    public string? AltEs { get; set; }
    public string? AltEn { get; set; }
    public string? AltDe { get; set; }
    public int? FallbackImageId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public PublicCatalogProduct? Product { get; set; }
    public PublicCatalogCategory? Category { get; set; }
    public PublicCatalogImage? FallbackImage { get; set; }
}
