namespace Revestik.Api.Services.Customers;

public sealed class DuplicateCustomerIdentificationException(
    Exception innerException)
    : Exception(
        "A customer with the same identification already exists.",
        innerException);