namespace Revestik.Shared.Sales;

public sealed class SaleResponse
{
    public int Id { get; set; }

    public string SaleNumber { get; set; } = string.Empty;

    public int? SourceQuotationId { get; set; }

    public string SourceQuotationNumber { get; set; } =
        string.Empty;

    public int CustomerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerIdentificationNumber { get; set; } =
        string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public string CustomerPhoneNumber { get; set; } =
        string.Empty;

    public Currency Currency { get; set; }

    public SaleStatus Status { get; set; }

    public DiscountType? GeneralDiscountType { get; set; }

    public decimal GeneralDiscountValue { get; set; }

    public DateTime? IssuedAtUtc { get; set; }

    public DateTime? VoidedAtUtc { get; set; }

    public string VoidReason { get; set; } = string.Empty;

    public int? ReplacesSaleId { get; set; }

    public int? ReplacementSaleId { get; set; }

    public string Observations { get; set; } = string.Empty;

    public string CreatedByUserId { get; set; } = string.Empty;

    public string CreatedByDisplayName { get; set; } =
        string.Empty;

    public string IssuedByDisplayName { get; set; } =
        string.Empty;

    public string VoidedByDisplayName { get; set; } =
        string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public List<SaleLineResponse> Lines { get; set; } = [];

    public List<SaleChargeResponse> Charges { get; set; } = [];

    public List<SalePaymentResponse> Payments { get; set; } = [];

    public decimal Subtotal { get; set; }

    public decimal LineDiscountTotal { get; set; }

    public decimal GeneralDiscountTotal { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal ChargeTotal { get; set; }

    public decimal Total { get; set; }

    public decimal PaidTotal { get; set; }

    public decimal OutstandingAmount { get; set; }

    public SaleBalanceStatus BalanceStatus { get; set; }

    public DateTime? NextPaymentDueAtUtc { get; set; }
}