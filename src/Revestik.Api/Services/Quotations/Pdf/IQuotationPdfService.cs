using Revestik.Shared.Quotations;

namespace Revestik.Api.Services.Quotations.Pdf;

public interface IQuotationPdfService
{
    byte[] Generate(QuotationResponse quotation);
}