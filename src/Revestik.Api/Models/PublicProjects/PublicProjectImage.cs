namespace Revestik.Api.Models.PublicProjects;

// Metadata only. Image bytes belong to durable public media storage, not SQL or private project files.
public sealed class PublicProjectImage
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public PublicProject Project { get; set; } = null!;
    public string Role { get; set; } = string.Empty; // ProjectCover or ProjectGallery
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public long FileSizeBytes { get; set; }
    public int DisplayOrder { get; set; }
    public string? AltEs { get; set; }
    public string? AltEn { get; set; }
    public string? AltDe { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<PublicProjectImageTag> Tags { get; set; } = new List<PublicProjectImageTag>();
}
