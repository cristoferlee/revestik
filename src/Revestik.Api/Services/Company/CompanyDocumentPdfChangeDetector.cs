namespace Revestik.Api.Services.Company;

// Captures field names only; never put customer text or PDF contents in the audit log.
public static class CompanyDocumentPdfChangeDetector
{
    public static IReadOnlyList<string> GetChangedFields(
        CompanyDocumentPdfPreferences previous, CompanyDocumentPdfPreferences current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        var changed = new List<string>(4);
        if (previous.FooterMessage != current.FooterMessage) changed.Add(nameof(current.FooterMessage));
        if (previous.QuotationDefaultTerms != current.QuotationDefaultTerms) changed.Add(nameof(current.QuotationDefaultTerms));
        if (previous.ShowWebsite != current.ShowWebsite) changed.Add(nameof(current.ShowWebsite));
        if (previous.ShowQuotationSeller != current.ShowQuotationSeller) changed.Add(nameof(current.ShowQuotationSeller));
        return changed;
    }
}
