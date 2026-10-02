namespace Revestik.Shared.Purchases;

public sealed record SupplierPurchaseCurrencySummaryResponse(
    PurchaseCurrency Currency,
    decimal OperationalPurchaseTotal,
    decimal OutstandingTotal);