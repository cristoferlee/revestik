using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;
using Revestik.Api.Configuration;
using Revestik.Api.Services.Sales.Pdf;
using Revestik.Shared.Sales;

namespace Revestik.Api.Tests.Sales;

public sealed class SalePdfServiceTests
{
    [Fact]
    public void Generate_WithIssuedSale_ReturnsPdfBytes()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var service = new SalePdfService(
            Options.Create(
                new CompanyOptions
                {
                    LegalName = "Revestik Test",
                    IdentificationNumber = "3101000000",
                    PhoneNumber = "22222222",
                    Email = "test@example.com",
                    Address = "Heredia",
                    Website = "revestik.test"
                }));

        var sale = new SaleResponse
        {
            Id = 1,
            SaleNumber = "VEN-000001",
            CustomerId = 1,
            CustomerName = "Cliente Test",
            CustomerIdentificationNumber = "3101234567",
            CustomerEmail = "cliente@example.com",
            CustomerPhoneNumber = "88888888",
            Currency = Currency.CRC,
            Status = SaleStatus.Issued,
            IssuedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByDisplayName = "Vendedor Test",
            Lines =
            [
                new SaleLineResponse
                {
                    Description = "Porcelanato",
                    Unit = "m²",
                    Quantity = 1m,
                    UnitPrice = 11300m,
                    TaxRate = 13m,
                    BaseAmount = 10000m,
                    TaxAmount = 1300m,
                    TotalAmount = 11300m
                }
            ],
            Subtotal = 10000m,
            TaxTotal = 1300m,
            Total = 11300m
        };

        var pdf = service.Generate(sale);

        Assert.NotEmpty(pdf);

        Assert.True(
            pdf.Length > 4);

        Assert.Equal(
            (byte)'%',
            pdf[0]);

        Assert.Equal(
            (byte)'P',
            pdf[1]);

        Assert.Equal(
            (byte)'D',
            pdf[2]);

        Assert.Equal(
            (byte)'F',
            pdf[3]);
    }
}