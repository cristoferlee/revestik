using Revestik.Api.Services.Company;
using Revestik.Shared.Sales;

namespace Revestik.Api.Services.Sales.Pdf;

public interface ISalePdfService
{
    byte[] Generate(SaleResponse sale);
    byte[] Generate(SaleResponse sale, CompanyPdfBranding branding);
    byte[] Generate(SaleResponse sale, CompanyPdfBranding branding, CompanyDocumentPdfPreferences preferences);
}
