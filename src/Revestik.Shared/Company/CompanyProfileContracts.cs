namespace Revestik.Shared.Company;

public sealed record CompanyProfileResponse(
    string LegalName, string IdentificationNumber,
    string TaxIdentificationType, string TaxIdentificationNumber,
    string CommercialName, string PhoneNumber, string Email, string Website,
    string ProvinceCode, string CantonCode, string DistrictCode,
    string OtherSigns, string Address, string Version);

// Intentionally excludes legal and fiscal identifiers and the image upload (CONFIG-02C).
public sealed record UpdateCompanyProfileRequest(
    string CommercialName, string PhoneNumber, string Email, string Website,
    string ProvinceCode, string CantonCode, string DistrictCode,
    string OtherSigns, string Version);
