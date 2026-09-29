using Revestik.Shared.Inventory;

namespace Revestik.Client.Services.Inventory;

public interface IInventoryApiService
{
    Task<InventorySummaryResponse> GetSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<InventoryMovementResponse?> RegisterInitialStockAsync(
        int productId,
        InitialStockRequest request,
        CancellationToken cancellationToken = default);

    Task<InventoryMovementResponse?> AdjustStockAsync(
        int productId,
        InventoryAdjustmentRequest request,
        CancellationToken cancellationToken = default);
}