namespace Revestik.Api.Models;

public sealed class ProjectDocument
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Category { get; set; } = "Other";
    public string DisplayName { get; set; } = string.Empty;
    public int? CurrentVersionId { get; set; }
    public long Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedByUserId { get; set; }
    public ICollection<ProjectDocumentVersion> Versions { get; set; } = [];
    public ICollection<ProjectDocumentEventLink> EventLinks { get; set; } = [];
}
