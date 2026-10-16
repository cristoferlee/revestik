namespace Revestik.Api.Models;

// Manual operational payment; never a SalePayment or accounting transaction.
public sealed class ProjectPayment
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public DateOnly PaidOn { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public long Version { get; set; } = 1;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? VoidedByUserId { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public string? VoidReason { get; set; }
    public ICollection<ProjectPaymentRevision> Revisions { get; set; } = [];
    public ICollection<ProjectPaymentDocumentLink> DocumentLinks { get; set; } = [];
}
