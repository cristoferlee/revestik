namespace Revestik.Shared.Sales;

public sealed class SaleSummaryResponse
{
    public List<SaleCurrencySummaryResponse> Currencies { get; set; } = [];
}