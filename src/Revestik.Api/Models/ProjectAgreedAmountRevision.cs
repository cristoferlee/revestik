namespace Revestik.Api.Models;

// Audit of changes to Project.AgreedAmount and Project.CurrencyCode.
public sealed class ProjectAgreedAmountRevision
{
    public long Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public long PreviousProjectVersion { get; set; }
    public decimal? PreviousAmount { get; set; }
    public decimal? NewAmount { get; set; }
    public string? PreviousCurrencyCode { get; set; }
    public string? NewCurrencyCode { get; set; }
    public string ChangedByUserId { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; } = string.Empty;
}
