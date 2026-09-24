using Revestik.Api.Models.Identity;
using Revestik.Shared.Sales;

namespace Revestik.Api.Models;

public sealed class SalePayment
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public DateTime PaidAtUtc { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public SalePaymentStatus Status { get; set; } = SalePaymentStatus.Active;
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public string? VoidedByUserId { get; set; }
    public ApplicationUser? VoidedByUser { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public string VoidReason { get; set; } = string.Empty;
}