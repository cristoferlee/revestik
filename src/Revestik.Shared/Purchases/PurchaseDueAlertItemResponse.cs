namespace Revestik.Shared.Purchases;

public sealed record PurchaseDueAlertItemResponse(
    int PurchaseId,
    int SupplierId,
    string SupplierName,
    PurchaseCurrency Currency,
    decimal OutstandingAmount,
    DateOnly DueDate,
    int DaysUntilDue,
    PurchaseDueAlertType AlertType);