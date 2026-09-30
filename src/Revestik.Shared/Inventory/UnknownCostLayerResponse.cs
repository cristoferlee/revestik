namespace Revestik.Shared.Inventory;

public sealed record UnknownCostLayerResponse(
    int LayerId,
    int ProductId,
    string ProductName,
    string InventoryUnitSymbol,
    decimal OriginalQuantity,
    decimal RemainingQuantity,
    int SourceMovementId,
    InventoryMovementType MovementType,
    InventoryAdjustmentReason? AdjustmentReason,
    int? PhysicalCountId,
    string Notes,
    DateTime CreatedAtUtc);