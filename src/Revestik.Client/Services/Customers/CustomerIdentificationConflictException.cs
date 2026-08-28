namespace Revestik.Client.Services.Customers;

public sealed class CustomerIdentificationConflictException
    : Exception
{
    public CustomerIdentificationConflictException()
        : base(
            "A customer with the same identification already exists.")
    {
    }
}