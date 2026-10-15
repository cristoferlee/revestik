namespace Revestik.Api.Models;

public sealed class ProjectMilestoneRevision
{
    public long Id { get; set; }
    public int MilestoneId { get; set; }
    public ProjectMilestone Milestone { get; set; } = null!;
    public long PreviousVersion { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public int SortOrder { get; set; }
    public bool WasExcluded { get; set; }
    public bool WasCompleted { get; set; }
    public DateTime? PreviousCompletedAtUtc { get; set; }
    public string? PreviousCompletedByUserId { get; set; }
    public string ChangeKind { get; set; } = string.Empty;
    public string ChangedByUserId { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
    public string? Reason { get; set; }
}
