namespace Revestik.Api.Services.Suppliers;

public sealed class DuplicateSupplierNameException(
    Exception? innerException = null)
    : Exception(
        "A supplier with the same name already exists.",
        innerException);