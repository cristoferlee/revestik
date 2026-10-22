namespace Revestik.Shared.Purchases;

public sealed record PurchasePeriodSummaryResponse(
    int Year,
    int Month,
    IReadOnlyList<PurchasePeriodCurrencySummaryResponse> Currencies);
