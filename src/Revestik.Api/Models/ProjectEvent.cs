namespace Revestik.Api.Models;

public sealed class ProjectEvent
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int CategoryId { get; set; }
    public ProjectEventCategory Category { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly OccurredOn { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedByUserId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsVoided { get; set; }
    public string? VoidReason { get; set; }
    public long Version { get; set; } = 1;
    public ICollection<ProjectEventRevision> Revisions { get; set; } = [];
}
