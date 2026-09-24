using Revestik.Shared.Sales;

namespace Revestik.Api.Services.Sales.Pdf;

public interface ISalePdfService
{
    byte[] Generate(SaleResponse sale);
}