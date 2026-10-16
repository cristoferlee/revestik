namespace Revestik.Api.Models;

public sealed class ProjectEventDeadlineRevision
{
    public long Id { get; set; }
    public int ProjectEventId { get; set; }
    public ProjectEvent ProjectEvent { get; set; } = null!;
    public long PreviousVersion { get; set; }
    public DateOnly? PreviousDueOn { get; set; }
    public string ChangedByUserId { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
}
