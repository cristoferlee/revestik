namespace Revestik.Api.Services.Inventory;

public sealed class InvalidInventoryUserException()
    : InvalidOperationException(
        "The authenticated user does not exist or is inactive.");
