using Revestik.Api.Models.Identity;
using Revestik.Shared.Sales;

namespace Revestik.Api.Models;

public sealed class Sale
{
    public int Id { get; set; }
    public string? SaleNumber { get; set; }
    public int? SourceQuotationId { get; set; }
    public Quotation? SourceQuotation { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string CustomerNameSnapshot { get; set; } = string.Empty;
    public string CustomerIdentificationNumberSnapshot { get; set; } = string.Empty;
    public string CustomerEmailSnapshot { get; set; } = string.Empty;
    public string CustomerPhoneNumberSnapshot { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public string? IssuedByUserId { get; set; }
    public ApplicationUser? IssuedByUser { get; set; }
    public string? VoidedByUserId { get; set; }
    public ApplicationUser? VoidedByUser { get; set; }
    public int? ReplacesSaleId { get; set; }
    public Sale? ReplacesSale { get; set; }
    public Sale? ReplacementSale { get; set; }
    public ICollection<SaleLine> Lines { get; set; } = [];
    public ICollection<SaleCharge> Charges { get; set; } = [];
    public ICollection<SalePayment> Payments { get; set; } = [];
    public Currency Currency { get; set; } = Currency.CRC;
    public SaleStatus Status { get; set; } = SaleStatus.Draft;
    public DiscountType? GeneralDiscountType { get; set; }
    public decimal GeneralDiscountValue { get; set; }
    public DateTime? IssuedAtUtc { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public string VoidReason { get; set; } = string.Empty;
    public string Observations { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}