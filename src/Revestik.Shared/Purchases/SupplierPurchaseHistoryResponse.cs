using Revestik.Shared.Common;

namespace Revestik.Shared.Purchases;

public sealed record SupplierPurchaseHistoryResponse(
    int SupplierId,
    string SupplierName,
    DateOnly? LastPurchaseDate,
    int PurchaseCount,
    IReadOnlyList<SupplierPurchaseCurrencySummaryResponse> Currencies,
    PaginatedResponse<PurchaseListItemResponse> Purchases);