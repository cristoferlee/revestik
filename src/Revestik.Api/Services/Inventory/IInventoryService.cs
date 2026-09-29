using Revestik.Shared.Inventory;

namespace Revestik.Api.Services.Inventory;

public interface IInventoryService
{
    Task<InventorySummaryResponse> GetSummaryAsync(
        CancellationToken cancellationToken);

    Task<InventoryMovementResponse?> RegisterInitialStockAsync(
        int productId,
        InitialStockRequest request,
        string createdByUserId,
        CancellationToken cancellationToken);

    Task<InventoryMovementResponse?> AdjustStockAsync(
        int productId,
        InventoryAdjustmentRequest request,
        string createdByUserId,
        CancellationToken cancellationToken);

    Task ConsumeSaleStockAsync(
        int productId,
        decimal commercialQuantity,
        string saleNumber,
        string createdByUserId,
        CancellationToken cancellationToken);
}