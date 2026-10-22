namespace Revestik.Shared.Sales;

public sealed class SalePeriodSummaryResponse
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<SalePeriodCurrencySummaryResponse> Currencies { get; set; } = [];
}
