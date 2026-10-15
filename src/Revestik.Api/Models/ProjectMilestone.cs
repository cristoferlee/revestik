namespace Revestik.Api.Models;

public sealed class ProjectMilestone
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public ProjectMilestonePlan Plan { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public int SortOrder { get; set; }
    public bool IsExcluded { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? CompletedByUserId { get; set; }
    public long Version { get; set; } = 1;
    public List<ProjectMilestoneRevision> Revisions { get; set; } = [];
}
