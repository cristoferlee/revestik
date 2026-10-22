namespace Revestik.Shared.ElectronicDocuments;

public sealed record DashboardDocumentedPurchaseCurrencyResponse(string CurrencyCode, decimal PurchasesTotal);
public sealed record DashboardDocumentedPurchasesSummaryResponse(
    int Year, int Month, IReadOnlyList<DashboardDocumentedPurchaseCurrencyResponse> Currencies);
