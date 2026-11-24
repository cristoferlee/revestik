namespace Revestik.Api.Models.PublicCatalog;

// Text-only editorial sections of /soluciones. No connection to PublicHomeSections.
public sealed class PublicSolutionsSection
{
    public string Key { get; set; } = "";
    public string ContentEs { get; set; } = "";
    public string? ContentEn { get; set; }
    public string? ContentDe { get; set; }
    // Photo storage belongs only to the Solutions page hero, not Home or any slide slot.
    public string? ImageStorageKey { get; set; }
    public string? ImageContentType { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
