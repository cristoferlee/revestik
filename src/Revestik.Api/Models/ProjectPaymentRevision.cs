namespace Revestik.Api.Models;

// Historical snapshot of payment before a manual correction.
public sealed class ProjectPaymentRevision
{
    public long Id { get; set; }
    public int ProjectPaymentId { get; set; }
    public ProjectPayment ProjectPayment { get; set; } = null!;
    public long PreviousVersion { get; set; }
    public decimal PreviousAmount { get; set; }
    public string PreviousCurrencyCode { get; set; } = string.Empty;
    public DateOnly PreviousPaidOn { get; set; }
    public string PreviousReference { get; set; } = string.Empty;
    public string PreviousNotes { get; set; } = string.Empty;
    public string ChangedByUserId { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; } = string.Empty;
}
