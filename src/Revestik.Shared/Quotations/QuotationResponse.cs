namespace Revestik.Shared.Quotations;

public sealed class QuotationResponse
{
    public int Id { get; set; }

    public string QuotationNumber { get; set; } = string.Empty;

    public int CustomerId { get; set; }

    public Currency Currency { get; set; }

    public DateTime IssuedAtUtc { get; set; }

    public DateTime? ValidUntilUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public List<QuotationLineResponse> Lines { get; set; } = [];

    public List<QuotationChargeResponse> Charges { get; set; } = [];

    public decimal Subtotal { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal ChargeTotal { get; set; }

    public decimal Total { get; set; }
}