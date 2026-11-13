namespace Revestik.Api.Models.PublicProjects;

// Public editorial portfolio content. Never use an operational Project or Customer as its source.
public sealed class PublicProject
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string TypeCode { get; set; } = string.Empty;
    public string? Location { get; set; }
    // Null means no manual ordering; the read API supplies the newest-first fallback.
    public int? DisplayOrder { get; set; }
    // Optional manually chosen Home position (1..3); null means not selected.
    public int? FeaturedOrder { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<PublicProjectTranslation> Translations { get; set; } = new List<PublicProjectTranslation>();
    public ICollection<PublicProjectImage> Images { get; set; } = new List<PublicProjectImage>();
    public ICollection<PublicProjectSpace> Spaces { get; set; } = new List<PublicProjectSpace>();
}
