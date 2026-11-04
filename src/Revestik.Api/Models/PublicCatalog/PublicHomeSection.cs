namespace Revestik.Api.Models.PublicCatalog;

// Editable copy for a bounded, known Home section, not arbitrary HTML or a page builder.
public sealed class PublicHomeSection
{
    public string Key { get; set; } = "";
    public string ContentEs { get; set; } = "";
    public string? ContentEn { get; set; }
    public string? ContentDe { get; set; }
    public string FeaturedSlugsJson { get; set; } = "[]";
    public string? ImageStorageKey { get; set; }
    public string? ImageContentType { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
