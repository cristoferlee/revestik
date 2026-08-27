namespace Revestik.Shared.Customers;

public sealed record CustomerListItemResponse(
    int Id,
    string IdentificationNumber,
    string Name,
    string Email,
    string PhoneNumber,
    IdentificationType IdentificationType,
    bool IsActive);