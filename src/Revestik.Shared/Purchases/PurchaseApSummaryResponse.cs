namespace Revestik.Shared.Purchases;

public sealed record PurchaseApSummaryResponse(
    IReadOnlyList<PurchaseCurrencyApSummaryResponse> Currencies);