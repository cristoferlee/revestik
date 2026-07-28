namespace Revestik.Shared.Customers;

public sealed record CustomerResponse(
    int Id,
    string Name,
    IdentificationType? IdentificationType,
    string? IdentificationNumber,
    string? Email,
    string? PhoneNumber,
    string? ProvinceCode,
    string? CantonCode,
    string? DistrictCode,
    string? OtherSigns,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);