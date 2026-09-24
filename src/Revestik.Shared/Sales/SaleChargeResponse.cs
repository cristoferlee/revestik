namespace Revestik.Shared.Sales;

public sealed class SaleChargeResponse
{
    public int Id { get; set; }

    public SaleChargeType Type { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}