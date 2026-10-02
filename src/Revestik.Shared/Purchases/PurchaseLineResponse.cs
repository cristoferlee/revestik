namespace Revestik.Shared.Purchases;

public sealed record PurchaseLineResponse(
    int Id,
    int ProductId,
    string ProductName,
    string InventoryUnitSymbol,
    decimal Quantity,
    decimal UnitCost);