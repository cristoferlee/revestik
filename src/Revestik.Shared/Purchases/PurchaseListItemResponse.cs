namespace Revestik.Shared.Purchases;

public sealed record PurchaseListItemResponse(
    int Id,
    DateOnly PurchaseDate,
    int SupplierId,
    string SupplierName,
    PurchaseCurrency Currency,
    PurchasePaymentType PaymentType,
    decimal Total,
    decimal PaidTotal,
    decimal OutstandingAmount,
    PurchaseBalanceStatus BalanceStatus,
    DateOnly? DueDate,
    bool IsOverdue);