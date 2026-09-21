using Revestik.Shared.Quotations;

namespace Revestik.Api.Models;

public sealed class QuotationCharge
{
    public int Id { get; set; }

    public int QuotationId { get; set; }

    public Quotation Quotation { get; set; } = null!;

    public QuotationChargeType Type { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}