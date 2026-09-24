namespace Revestik.Shared.Sales;

public sealed class SalePaymentResponse
{
    public int Id { get; set; }

    public decimal Amount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public DateTime PaidAtUtc { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public SalePaymentStatus Status { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public string CreatedByDisplayName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public string VoidedByDisplayName { get; set; } = string.Empty;

    public DateTime? VoidedAtUtc { get; set; }

    public string VoidReason { get; set; } = string.Empty;
}