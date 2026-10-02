namespace Revestik.Shared.Suppliers;

public sealed record SupplierListItemResponse(
    int Id,
    string Name,
    string ContactName,
    string PhoneNumber,
    string Email,
    bool IsActive);