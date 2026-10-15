namespace Revestik.Api.Models;

public sealed class ProjectMilestonePlan
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public long Version { get; set; } = 1;
    public bool IsActive { get; set; }
    public List<ProjectMilestone> Milestones { get; set; } = [];
}
