namespace Revestik.Api.Services.Inventory;

public sealed class InvalidInventoryOperationException(string message)
    : InvalidOperationException(message);