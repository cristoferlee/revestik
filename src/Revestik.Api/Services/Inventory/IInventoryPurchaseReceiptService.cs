namespace Revestik.Api.Services.Inventory;

public interface IInventoryPurchaseReceiptService
{
    Task ReceiveAsync(
        int purchaseLineId,
        decimal inventoryUnitCost,
        string createdByUserId,
        CancellationToken cancellationToken);
}