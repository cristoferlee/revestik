namespace Revestik.Api.Models.PublicCatalog;

public sealed class PublicCatalogProductTranslation
{
    public int ProductId { get; set; }
    public PublicCatalogProduct Product { get; set; } = null!;
    public string LanguageCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
}
