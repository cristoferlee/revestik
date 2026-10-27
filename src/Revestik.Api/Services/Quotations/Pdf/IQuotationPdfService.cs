using Revestik.Api.Services.Company;
using Revestik.Shared.Quotations;

namespace Revestik.Api.Services.Quotations.Pdf;

public interface IQuotationPdfService
{
    byte[] Generate(QuotationResponse quotation);
    byte[] Generate(QuotationResponse quotation, CompanyPdfBranding branding);
    byte[] Generate(QuotationResponse quotation, CompanyPdfBranding branding, CompanyDocumentPdfPreferences preferences);
}
