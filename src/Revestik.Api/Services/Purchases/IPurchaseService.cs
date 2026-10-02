using Revestik.Shared.Purchases;

namespace Revestik.Api.Services.Purchases;

public interface IPurchaseService
{
    Task<PurchaseResponse> CreateAsync(
        PurchaseCreateRequest request,
        string createdByUserId,
        CancellationToken cancellationToken);

    Task<PurchaseResponse?> RegisterPaymentAsync(
        int purchaseId,
        PurchasePaymentRequest request,
        string createdByUserId,
        CancellationToken cancellationToken);

    Task<PurchaseResponse?> VoidPaymentAsync(
        int purchaseId,
        int paymentId,
        VoidPurchasePaymentRequest request,
        string voidedByUserId,
        CancellationToken cancellationToken);

    Task<PurchaseResponse?> GetByIdAsync(
        int purchaseId,
        CancellationToken cancellationToken);
}