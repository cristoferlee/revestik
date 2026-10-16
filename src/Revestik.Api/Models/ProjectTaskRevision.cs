namespace Revestik.Api.Models;

public sealed class ProjectTaskRevision
{
    public long Id { get; set; }
    public int ProjectTaskId { get; set; }
    public ProjectTask ProjectTask { get; set; } = null!;
    public long PreviousVersion { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? AssignedToUserId { get; set; }
    public DateOnly? DueOn { get; set; }
    public bool WasCompleted { get; set; }
    public DateTime? PreviousCompletedAtUtc { get; set; }
    public string? PreviousCompletedByUserId { get; set; }
    public string ChangedByUserId { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
}
