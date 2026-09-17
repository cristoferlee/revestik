using Revestik.Shared.Quotations;
namespace Revestik.Api.Models;

public sealed class QuotationLine
{
    public int Id { get; set; }

    public int QuotationId { get; set; }

    public Quotation Quotation { get; set; } = null!;

    public int? ProductId { get; set; }

    public string CabysCode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public DiscountType? DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public decimal TaxRate { get; set; } = 13m;
}