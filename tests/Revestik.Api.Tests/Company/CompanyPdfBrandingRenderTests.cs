using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;
using Revestik.Api.Configuration;
using Revestik.Api.Services.Company;
using Revestik.Api.Services.Sales.Pdf;
using Revestik.Api.Services.Quotations.Pdf;
using Revestik.Shared.Sales;
using Revestik.Shared.Quotations;
using SaleCurrency = Revestik.Shared.Sales.Currency;
using QuotationCurrency = Revestik.Shared.Quotations.Currency;

namespace Revestik.Api.Tests.Company;

public sealed class CompanyPdfBrandingRenderTests
{
    private static CompanyPdfBranding Brand() => new(
        new CompanyDocumentIdentity
        {
            LegalName = "Empresa Histórica S.R.L.",
            IdentificationNumber = "3101000000",
            CommercialName = "Empresa Histórica",
            PhoneNumber = "22222222",
            Email = "historico@example.com",
            Address = "Heredia",
            Website = "historico.example"
        },
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"64\" height=\"32\"><rect width=\"64\" height=\"32\" fill=\"orange\"/></svg>",
        null);

    [Fact]
    public void SalePdf_WithPreparedHistoricalBranding_GeneratesPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var service = new SalePdfService(Options.Create(new CompanyOptions()));
        var sale = new SaleResponse
        {
            Id = 1,
            SaleNumber = "VEN-000001",
            CustomerId = 1,
            CustomerName = "Cliente",
            CustomerIdentificationNumber = "3101234567",
            Currency = SaleCurrency.CRC,
            Status = SaleStatus.Issued,
            IssuedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByDisplayName = "Vendedor",
            Lines = [new SaleLineResponse
            {
                Description = "Producto", Unit = "m²", Quantity = 1,
                UnitPrice = 11300, TaxRate = 13, BaseAmount = 10000,
                TaxAmount = 1300, TotalAmount = 11300
            }],
            Subtotal = 10000, TaxTotal = 1300, Total = 11300
        };
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(service.Generate(sale, Brand())[..4]));
    }

    [Fact]
    public void QuotationPdf_WithPreparedHistoricalBranding_GeneratesPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var service = new QuotationPdfService(Options.Create(new CompanyOptions()));
        var quotation = new QuotationResponse
        {
            Id = 1,
            QuotationNumber = "COT-000001",
            CustomerId = 1,
            CustomerName = "Cliente",
            CustomerIdentificationNumber = "3101234567",
            Currency = QuotationCurrency.CRC,
            Status = QuotationStatus.Issued,
            IssuedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByDisplayName = "Vendedor",
            Lines = [], Charges = []
        };
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(service.Generate(quotation, Brand())[..4]));
    }
}
