namespace Revestik.Shared.Inventory;

public sealed record PhysicalCountLineResponse(
    int Id,
    int ProductId,
    string ProductName,
    string InventoryUnitSymbol,
    bool RequiresWholeInventoryUnits,
    decimal ExpectedQuantity,
    decimal? CountedQuantity,
    decimal? DifferenceQuantity);
