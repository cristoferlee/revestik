namespace Revestik.Api.Models;

// Single-company configuration. Fiscal identifiers are deliberately not accepted by the update API.
public sealed class CompanyProfile
{
    public int Id { get; set; } = 1;
    public string LegalName { get; set; } = string.Empty;
    public string IdentificationNumber { get; set; } = string.Empty;
    public string TaxIdentificationType { get; set; } = string.Empty;
    public string TaxIdentificationNumber { get; set; } = string.Empty;
    public string CommercialName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string ProvinceCode { get; set; } = string.Empty;
    public string CantonCode { get; set; } = string.Empty;
    public string DistrictCode { get; set; } = string.Empty;
    public string OtherSigns { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty; // Existing unstructured address, preserved during first-time initialization.
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedByUserId { get; set; }
    public string? LogoStorageKey { get; set; }
    public string? LogoContentType { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
