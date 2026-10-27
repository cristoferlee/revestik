namespace Revestik.Api.Models;

public sealed class CompanyDocumentPdfSetting
{
    public int Id { get; set; } = 1;
    public string FooterMessage { get; set; } = string.Empty;
    public string QuotationDefaultTerms { get; set; } = string.Empty;
    public bool ShowWebsite { get; set; } = true;
    public bool ShowQuotationSeller { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
}
