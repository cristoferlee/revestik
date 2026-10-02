namespace Revestik.Shared.Inventory;

public sealed record InventoryCostLayerResponse(
    int LayerId,
    int ProductId,
    string ProductName,
    string InventoryUnitSymbol,
    decimal OriginalQuantity,
    decimal RemainingQuantity,
    decimal? UnitCost,
    int SourceMovementId,
    InventoryMovementType MovementType,
    int? PurchaseId,
    int? SaleId,
    int? PhysicalCountId,
    DateTime CreatedAtUtc);