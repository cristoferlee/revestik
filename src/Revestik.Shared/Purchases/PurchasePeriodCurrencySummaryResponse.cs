namespace Revestik.Shared.Purchases;

public sealed record PurchasePeriodCurrencySummaryResponse(
    PurchaseCurrency Currency,
    int PurchaseCount,
    decimal PurchasedTotal,
    int CashPurchaseCount,
    decimal CashPurchasedTotal,
    int CreditPurchaseCount,
    decimal CreditPurchasedTotal);
