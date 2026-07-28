using Revestik.Shared.Customers;

namespace Revestik.Api.Models;

public sealed class Customer
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public IdentificationType? IdentificationType { get; set; }

    public string? IdentificationNumber { get; set; }

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public string? ProvinceCode { get; set; }

    public string? CantonCode { get; set; }

    public string? DistrictCode { get; set; }

    public string? OtherSigns { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}