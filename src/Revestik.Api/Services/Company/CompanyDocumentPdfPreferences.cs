namespace Revestik.Api.Services.Company;

// Immutable values used when rendering a document. Capture these at issuance.
// Defaults preserve the existing PDF presentation for historical documents.
public sealed record CompanyDocumentPdfPreferences
{
    public string FooterMessage { get; init; } = string.Empty;
    public string QuotationDefaultTerms { get; init; } = string.Empty;
    public bool ShowWebsite { get; init; } = true;
    public bool ShowQuotationSeller { get; init; } = true;
}
