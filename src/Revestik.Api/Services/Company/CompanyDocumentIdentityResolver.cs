using Revestik.Api.Configuration;
using Revestik.Api.Models;

namespace Revestik.Api.Services.Company;

// Pure policy for document identity. Database loading and historical logo file
// retrieval belong to the application/PDF orchestration in CONFIG-02D-02/04.
public static class CompanyDocumentIdentityResolver
{
    public static CompanyDocumentIdentity Capture(CompanyProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new CompanyDocumentIdentity
        {
            LegalName = profile.LegalName,
            IdentificationNumber = profile.IdentificationNumber,
            TaxIdentificationType = profile.TaxIdentificationType,
            TaxIdentificationNumber = profile.TaxIdentificationNumber,
            CommercialName = profile.CommercialName,
            PhoneNumber = profile.PhoneNumber,
            Email = profile.Email,
            Website = profile.Website,
            Address = profile.Address,
            ProvinceCode = profile.ProvinceCode,
            CantonCode = profile.CantonCode,
            DistrictCode = profile.DistrictCode,
            OtherSigns = profile.OtherSigns,
            LogoStorageKey = profile.LogoStorageKey,
            LogoContentType = profile.LogoContentType
        };
    }

    public static CompanyDocumentIdentity FromLegacyOptions(CompanyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CompanyDocumentIdentity
        {
            LegalName = options.LegalName,
            IdentificationNumber = options.IdentificationNumber,
            TaxIdentificationType = options.TaxIdentificationType,
            TaxIdentificationNumber = options.TaxIdentificationNumber,
            PhoneNumber = options.PhoneNumber,
            Email = options.Email,
            Website = options.Website,
            Address = options.Address
            // Legacy documents always use the packaged default logo.
        };
    }

    // A document is issued only when its domain status says so. In particular,
    // a voided sale is still an issued document and retains its snapshot.
    public static CompanyDocumentIdentity Resolve(
        bool isIssued,
        CompanyDocumentIdentity? issuedSnapshot,
        CompanyProfile? currentProfile,
        CompanyOptions legacyOptions)
    {
        ArgumentNullException.ThrowIfNull(legacyOptions);
        if (isIssued)
            return issuedSnapshot ?? FromLegacyOptions(legacyOptions);
        return currentProfile is not null
            ? Capture(currentProfile)
            : FromLegacyOptions(legacyOptions);
    }
}
