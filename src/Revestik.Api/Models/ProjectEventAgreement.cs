namespace Revestik.Api.Models;

// Optional agreement details belonging to one event.
public sealed class ProjectEventAgreement
{
    public int ProjectEventId { get; set; }
    public ProjectEvent ProjectEvent { get; set; } = null!;
    public string Status { get; set; } = "Pending";
    public string? AcceptedBy { get; set; }
    public DateOnly? AcceptedOn { get; set; }
    public string? AcceptanceMedium { get; set; }
    public string? Notes { get; set; }
    public long Version { get; set; } = 1;
    public DateTime UpdatedAtUtc { get; set; }
    public string UpdatedByUserId { get; set; } = string.Empty;
}
