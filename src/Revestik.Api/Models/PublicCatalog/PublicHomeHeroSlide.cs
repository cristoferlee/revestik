namespace Revestik.Api.Models.PublicCatalog;

// A fixed slot (1..3) on the public Home page. No draft or inventory coupling.
public sealed class PublicHomeHeroSlide
{
    public int Slot { get; set; }
    public string HeadingEs { get; set; } = "";
    public string? HeadingEn { get; set; }
    public string? HeadingDe { get; set; }
    public string DescriptionEs { get; set; } = "";
    public string? DescriptionEn { get; set; }
    public string? DescriptionDe { get; set; }
    public string? ImageStorageKey { get; set; }
    public string? ImageContentType { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
