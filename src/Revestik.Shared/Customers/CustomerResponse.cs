namespace Revestik.Shared.Customers;

public sealed record CustomerResponse(
    int Id,
    string Name,
    string? IdentificationNumber,
    string? Email,
    string? PhoneNumber,
    string? Address,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);