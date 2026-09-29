namespace Revestik.Api.Services.Inventory;

public sealed class InventoryConcurrencyException()
    : InvalidOperationException(
        "The product inventory changed during the operation. Refresh the data and try again.");