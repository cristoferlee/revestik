namespace Revestik.Api.Models;

// Historical snapshot of an agreement before an update.
public sealed class ProjectEventAgreementRevision
{
    public long Id { get; set; }
    public int ProjectEventId { get; set; }
    public ProjectEvent ProjectEvent { get; set; } = null!;
    public long PreviousVersion { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AcceptedBy { get; set; }
    public DateOnly? AcceptedOn { get; set; }
    public string? AcceptanceMedium { get; set; }
    public string? Notes { get; set; }
    public string ChangedByUserId { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
}
