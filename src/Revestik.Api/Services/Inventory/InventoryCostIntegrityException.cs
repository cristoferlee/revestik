namespace Revestik.Api.Services.Inventory;

public sealed class InventoryCostIntegrityException(string message)
    : InvalidOperationException(message);