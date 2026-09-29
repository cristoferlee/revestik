namespace Revestik.Shared.Inventory;

public sealed record InventoryMovementResponse(
    int Id,
    int ProductId,
    InventoryMovementType Type,
    decimal QuantityChange,
    decimal StockBefore,
    decimal StockAfter,
    decimal? UnitCost,
    InventoryAdjustmentReason? AdjustmentReason,
    string Notes,
    string CreatedByUserId,
    DateTime CreatedAtUtc);