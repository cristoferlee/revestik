namespace Revestik.Api.Services.Company;

// Value captured at issuance. A null LogoStorageKey explicitly means the default
// packaged Revestik SVG, NOT the currently configured company logo.
// A null CompanyDocumentIdentity on an issued document means a legacy document.
public sealed record CompanyDocumentIdentity
{
    public string LegalName { get; init; } = string.Empty;
    public string IdentificationNumber { get; init; } = string.Empty;
    public string TaxIdentificationType { get; init; } = string.Empty;
    public string TaxIdentificationNumber { get; init; } = string.Empty;
    public string CommercialName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Website { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string ProvinceCode { get; init; } = string.Empty;
    public string CantonCode { get; init; } = string.Empty;
    public string DistrictCode { get; init; } = string.Empty;
    public string OtherSigns { get; init; } = string.Empty;
    public string? LogoStorageKey { get; init; }
    public string? LogoContentType { get; init; }
}
