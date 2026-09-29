namespace Revestik.Api.Services.Inventory;

public sealed class InitialStockAlreadyRegisteredException()
    : InvalidOperationException(
        "Initial stock has already been registered for this product.");