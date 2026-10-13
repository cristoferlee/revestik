namespace Revestik.Api.Models;

// Immutable snapshot of the previous event state, written in the same transaction as its change.
public sealed class ProjectEventRevision
{
    public long Id { get; set; }
    public int ProjectEventId { get; set; }
    public ProjectEvent ProjectEvent { get; set; } = null!;
    public long PreviousVersion { get; set; }
    public int CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly OccurredOn { get; set; }
    public bool WasVoided { get; set; }
    public string? PreviousVoidReason { get; set; }
    public string ChangedByUserId { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
    public string ChangeKind { get; set; } = string.Empty;
}
