using Revestik.Shared.Sales;

namespace Revestik.Client.Pages;

public sealed class SaleChargeFormModel
{
    public SaleChargeType Type { get; set; } =
        SaleChargeType.Transport;

    public string Description { get; set; } =
        string.Empty;

    public decimal Amount { get; set; }

    public SaleChargeRequest ToRequest()
    {
        return new SaleChargeRequest
        {
            Type = Type,
            Description = Description.Trim(),
            Amount = Amount
        };
    }
}