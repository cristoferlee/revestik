namespace Revestik.Api.Models;

// An optional due date on a manual timeline event (including agreements and incidents).
public sealed class ProjectEventDeadline
{
    public int ProjectEventId { get; set; }
    public ProjectEvent ProjectEvent { get; set; } = null!;
    public DateOnly? DueOn { get; set; }
    public long Version { get; set; } = 1;
    public string ChangedByUserId { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
}
