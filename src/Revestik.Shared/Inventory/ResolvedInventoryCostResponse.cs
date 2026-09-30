namespace Revestik.Shared.Inventory;

public sealed record ResolvedInventoryCostResponse(
    int LayerId,
    int ProductId,
    string ProductName,
    decimal RemainingQuantity,
    decimal ResolvedUnitCost,
    string ResolvedByUserId,
    DateTime ResolvedAtUtc);