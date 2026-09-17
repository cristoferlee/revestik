namespace Revestik.Shared.Quotations;

public sealed class QuotationChargeResponse
{
    public int Id { get; set; }

    public QuotationChargeType Type { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public decimal TaxRate { get; set; }
}