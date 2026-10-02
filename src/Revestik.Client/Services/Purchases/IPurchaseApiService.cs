using Revestik.Shared.Common;
using Revestik.Shared.Purchases;

namespace Revestik.Client.Services.Purchases;

public interface IPurchaseApiService
{
    Task<PaginatedResponse<PurchaseListItemResponse>> GetPageAsync(
        PurchaseListRequest request,
        CancellationToken cancellationToken = default);

    Task<PurchaseApSummaryResponse> GetApSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<PurchaseDueAlertResponse> GetDueAlertsAsync(
        PurchaseAlertRequest request,
        CancellationToken cancellationToken = default);

    Task<SupplierPurchaseHistoryResponse?> GetSupplierHistoryAsync(
        int supplierId,
        SupplierPurchaseHistoryRequest request,
        CancellationToken cancellationToken = default);

    Task<PurchaseResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<PurchaseResponse> CreateAsync(
        PurchaseCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<PurchaseResponse?> RegisterPaymentAsync(
        int id,
        PurchasePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<PurchaseResponse?> VoidPaymentAsync(
        int id,
        int paymentId,
        VoidPurchasePaymentRequest request,
        CancellationToken cancellationToken = default);
}