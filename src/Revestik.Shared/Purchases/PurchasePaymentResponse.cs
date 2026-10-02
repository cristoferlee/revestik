using Revestik.Shared.Sales;

namespace Revestik.Shared.Purchases;

public sealed record PurchasePaymentResponse(
    int Id,
    decimal Amount,
    PaymentMethod PaymentMethod,
    DateTime PaidAtUtc,
    decimal? ExchangeRate,
    string Reference,
    string Notes,
    PurchasePaymentStatus Status,
    string CreatedByUserId,
    string CreatedByDisplayName,
    DateTime CreatedAtUtc,
    string VoidedByDisplayName,
    DateTime? VoidedAtUtc,
    string VoidReason);