using Revestik.Shared.Common;
using Revestik.Shared.Purchases;

namespace Revestik.Api.Services.Purchases;

public interface IPurchaseQueryService
{
    Task<PaginatedResponse<PurchaseListItemResponse>> GetPageAsync(
        PurchaseListRequest request,
        CancellationToken cancellationToken);

    Task<PurchaseApSummaryResponse> GetApSummaryAsync(
        CancellationToken cancellationToken);

    Task<PurchaseDueAlertResponse> GetDueAlertsAsync(
        PurchaseAlertRequest request,
        CancellationToken cancellationToken);

    Task<SupplierPurchaseHistoryResponse?> GetSupplierHistoryAsync(
        int supplierId,
        SupplierPurchaseHistoryRequest request,
        CancellationToken cancellationToken);
}