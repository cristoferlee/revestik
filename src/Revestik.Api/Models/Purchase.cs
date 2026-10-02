using Revestik.Api.Models.Identity;
using Revestik.Shared.Purchases;

namespace Revestik.Api.Models;

public sealed class Purchase
{
    public int Id { get; set; }

    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public DateOnly PurchaseDate { get; set; }

    public PurchaseCurrency Currency { get; set; } = PurchaseCurrency.CRC;

    public decimal? ExchangeRate { get; set; }

    public PurchasePaymentType PaymentType { get; set; } = PurchasePaymentType.Cash;

    public int? CreditTermDays { get; set; }

    public DateOnly? DueDate { get; set; }

    public string Notes { get; set; } = string.Empty;

    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser CreatedByUser { get; set; } = null!;

    public ICollection<PurchaseLine> Lines { get; set; } = [];

    public ICollection<PurchasePayment> Payments { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }
}