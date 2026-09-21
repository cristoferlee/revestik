using Revestik.Shared.Quotations;

namespace Revestik.Client.Pages;

public sealed class QuotationChargeFormModel
{
    public QuotationChargeType Type { get; set; } =
        QuotationChargeType.Transport;

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public QuotationChargeRequest ToRequest()
    {
        return new QuotationChargeRequest
        {
            Type = Type,
            Description = Description.Trim(),
            Amount = Amount
        };
    }
}