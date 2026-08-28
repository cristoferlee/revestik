using Revestik.Shared.Customers;

namespace Revestik.Api.Models;

public sealed class Customer
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public IdentificationType IdentificationType { get; set; }

    public string IdentificationNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string ProvinceCode { get; set; } = string.Empty;

    public string CantonCode { get; set; } = string.Empty;

    public string DistrictCode { get; set; } = string.Empty;

    public string OtherSigns { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}