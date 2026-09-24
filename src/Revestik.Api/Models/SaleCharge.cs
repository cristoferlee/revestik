using Revestik.Shared.Sales;

namespace Revestik.Api.Models;

public sealed class SaleCharge
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;
    public SaleChargeType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}