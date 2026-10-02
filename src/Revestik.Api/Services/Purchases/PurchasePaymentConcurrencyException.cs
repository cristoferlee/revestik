namespace Revestik.Api.Services.Purchases;

public sealed class PurchasePaymentConcurrencyException(
    string message =
        "The purchase payment was changed by another operation.")
    : InvalidOperationException(message);