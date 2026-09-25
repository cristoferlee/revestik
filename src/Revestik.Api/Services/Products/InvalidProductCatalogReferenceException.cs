namespace Revestik.Api.Services.Products;

public sealed class InvalidProductCatalogReferenceException(string message)
    : Exception(message)
{
}