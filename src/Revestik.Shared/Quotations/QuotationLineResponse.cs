namespace Revestik.Shared.Quotations;

public sealed class QuotationLineResponse
{
    public int Id { get; set; }

    public int? ProductId { get; set; }

    public string CabysCode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public DiscountType? DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public decimal TaxRate { get; set; }

    public decimal BaseAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }
}