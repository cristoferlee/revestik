namespace Revestik.Api.Services.Inventory;

public sealed class PurchaseInventoryAlreadyAppliedException(
    string message =
        "The purchase line has already been applied to inventory.")
    : InvalidOperationException(message);