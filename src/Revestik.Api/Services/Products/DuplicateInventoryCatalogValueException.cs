namespace Revestik.Api.Services.Products;

public sealed class DuplicateInventoryCatalogValueException(
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
}