namespace Revestik.Shared.Sales;

public sealed class SaleCurrencySummaryResponse
{
    public Currency Currency { get; set; }

    public decimal SoldTotal { get; set; }

    public decimal CollectedTotal { get; set; }

    public decimal OutstandingTotal { get; set; }

    public int SaleCount { get; set; }
}