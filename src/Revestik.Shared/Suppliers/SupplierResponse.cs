namespace Revestik.Shared.Suppliers;

public sealed record SupplierResponse(
    int Id,
    string Name,
    string ContactName,
    string PhoneNumber,
    string Email,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);