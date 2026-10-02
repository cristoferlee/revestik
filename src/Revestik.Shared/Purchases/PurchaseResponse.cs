namespace Revestik.Shared.Purchases;

public sealed record PurchaseResponse(
    int Id,
    int SupplierId,
    string SupplierName,
    DateOnly PurchaseDate,
    PurchaseCurrency Currency,
    decimal? ExchangeRate,
    PurchasePaymentType PaymentType,
    int? CreditTermDays,
    DateOnly? DueDate,
    string Notes,
    decimal Total,
    decimal PaidTotal,
    decimal OutstandingAmount,
    PurchaseBalanceStatus BalanceStatus,
    bool IsOverdue,
    string CreatedByUserId,
    string CreatedByDisplayName,
    DateTime CreatedAtUtc,
    IReadOnlyList<PurchaseLineResponse> Lines,
    IReadOnlyList<PurchasePaymentResponse> Payments);