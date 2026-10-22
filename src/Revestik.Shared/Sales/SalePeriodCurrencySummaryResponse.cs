namespace Revestik.Shared.Sales;

public sealed class SalePeriodCurrencySummaryResponse
{
    public Currency Currency { get; set; }
    public decimal SalesTotal { get; set; }
    public int SaleCount { get; set; }
    public decimal PreviousMonthSalesTotal { get; set; }
    public int PreviousMonthSaleCount { get; set; }
    public decimal CollectedTotal { get; set; }
    public decimal? SalesChangePercent { get; set; }
}
