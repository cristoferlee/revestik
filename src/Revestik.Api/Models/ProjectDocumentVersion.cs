namespace Revestik.Api.Models;

public sealed class ProjectDocumentVersion
{
    public int Id { get; set; }
    public int ProjectDocumentId { get; set; }
    public ProjectDocument Document { get; set; } = null!;
    public int Number { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256Hex { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string? ThumbnailStorageKey { get; set; }
    public string State { get; set; } = "Pending";
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    public string UploadedByUserId { get; set; } = string.Empty;
}
