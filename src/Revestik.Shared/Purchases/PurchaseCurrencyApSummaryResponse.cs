namespace Revestik.Shared.Purchases;

public sealed record PurchaseCurrencyApSummaryResponse(
    PurchaseCurrency Currency,
    int PurchaseCount,
    decimal OutstandingTotal,
    int OverdueCount,
    decimal OverdueTotal);