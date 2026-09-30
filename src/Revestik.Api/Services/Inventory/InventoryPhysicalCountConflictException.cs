namespace Revestik.Api.Services.Inventory;

public sealed class InventoryPhysicalCountConflictException(string message)
    : InvalidOperationException(message);
