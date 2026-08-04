namespace Revestik.Shared.Taxpayers;

public sealed record TaxpayerLookupResponse(
    string IdentificationNumber,
    string IdentificationTypeCode,
    string Name,
    string TaxRegime,
    string TaxStatus,
    bool IsDelinquent,
    bool IsNonFiler,
    string TaxAdministration);