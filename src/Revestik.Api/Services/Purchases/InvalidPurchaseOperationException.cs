namespace Revestik.Api.Services.Purchases;

public sealed class InvalidPurchaseOperationException(
    string message)
    : InvalidOperationException(message);