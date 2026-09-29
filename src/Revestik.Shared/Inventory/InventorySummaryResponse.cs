namespace Revestik.Shared.Inventory;

public sealed record InventorySummaryResponse(
    int TotalProducts,
    int InStockCount,
    int LowStockCount,
    int OutOfStockCount,
    decimal TotalInventoryCostValue,
    decimal UnknownCostQuantity,
    int ProductsWithUnknownCost);