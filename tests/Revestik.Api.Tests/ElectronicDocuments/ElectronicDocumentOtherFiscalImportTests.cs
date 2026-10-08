
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Revestik.Api.Configuration;
using Revestik.Api.Data;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Api.Services.HaciendaXml;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class ElectronicDocumentOtherFiscalImportTests
{
    [Theory]
    [InlineData(HaciendaXmlDocumentKind.FacturaElectronicaCompra, ElectronicDocumentType.PurchaseInvoice)]
    [InlineData(HaciendaXmlDocumentKind.FacturaElectronicaExportacion, ElectronicDocumentType.ExportInvoice)]
    [InlineData(HaciendaXmlDocumentKind.ReciboElectronicoPago, ElectronicDocumentType.ElectronicPaymentReceipt)]
    public async Task Import_FiscalEvidence_DoesNotCreateEconomicMovements(
        HaciendaXmlDocumentKind kind,
        ElectronicDocumentType expectedType)
    {
        await using var db = CreateDb();
        await SeedUser(db);

        var parsed = FiscalDocument(kind);
        var service = Service(db, parsed);

        await service.ImportAsync(
            [1, 2, 3],
            "tester",
            CancellationToken.None);

        var saved = await db.ElectronicDocuments.SingleAsync();

        Assert.Equal(expectedType, saved.DocumentType);
        Assert.Equal(ElectronicDocumentDirection.Received, saved.Direction);
        Assert.Equal(ElectronicDocumentProcessingStatus.Pending, saved.ProcessingStatus);
        Assert.Null(saved.AdjustmentStatus);
        Assert.Null(saved.CategoryId);
        Assert.Null(saved.PurchaseId);
        Assert.Null(saved.SupplierId);
        Assert.Equal(25m, saved.TotalDocument);

        Assert.Empty(db.Purchases);
        Assert.Empty(db.Expenses);
        Assert.Empty(db.PurchasePayments);
    }

    [Fact]
    public async Task Import_ReceivedDocumentAddressedElsewhere_IsRejected()
    {
        await using var db = CreateDb();
        await SeedUser(db);

        var parsed = FiscalDocument(
            HaciendaXmlDocumentKind.FacturaElectronicaCompra) with
        {
            Receiver = new ParsedHaciendaParty(
                "Other",
                "02",
                "3101999999",
                "",
                "")
        };

        await Assert.ThrowsAsync<InvalidElectronicDocumentReceiverException>(
            () => Service(db, parsed).ImportAsync(
                [1],
                "tester",
                CancellationToken.None));

        Assert.Empty(db.ElectronicDocuments);
    }

    [Fact]
    public async Task Import_FiscalEvidence_RejectsDuplicateClave()
    {
        await using var db = CreateDb();
        await SeedUser(db);

        var service = Service(
            db,
            FiscalDocument(HaciendaXmlDocumentKind.ReciboElectronicoPago));

        await service.ImportAsync(
            [1],
            "tester",
            CancellationToken.None);

        await Assert.ThrowsAsync<DuplicateElectronicDocumentException>(
            () => service.ImportAsync(
                [1],
                "tester",
                CancellationToken.None));

        Assert.Single(db.ElectronicDocuments);
    }

    private static RevestikDbContext CreateDb() => new(
        new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"OtherFiscal-{Guid.NewGuid()}")
            .Options);

    private static async Task SeedUser(RevestikDbContext db)
    {
        db.Users.Add(new ApplicationUser
        {
            Id = "tester",
            UserName = "tester@example.com",
            NormalizedUserName = "TESTER@EXAMPLE.COM",
            Email = "tester@example.com",
            NormalizedEmail = "TESTER@EXAMPLE.COM",
            DisplayName = "Tester",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private static ElectronicDocumentImportService Service(
        RevestikDbContext db,
        ParsedHaciendaDocument document) =>
        new(
            db,
            new FixedParser(document),
            Options.Create(new CompanyOptions
            {
                TaxIdentificationType = "02",
                TaxIdentificationNumber = "3102959852"
            }));

    private sealed class FixedParser(ParsedHaciendaDocument result)
        : IElectronicDocumentXmlParser
    {
        public ParsedReceivedXml Parse(ReadOnlyMemory<byte> xml) => result;
    }

    private static ParsedHaciendaDocument FiscalDocument(
        HaciendaXmlDocumentKind kind) => new(
            kind,
            "50602102600310115777600500001010000051684120639723",
            "00500001010000051684",
            new DateTimeOffset(
                2026, 10, 2, 11, 12, 56, TimeSpan.FromHours(-6)),
            new ParsedHaciendaParty(
                "Supplier", "02", "3101157776", "", ""),
            new ParsedHaciendaParty(
                "Revestik", "02", "3102959852", "", ""),
            "",
            "",
            "01",
            null,
            "CRC",
            1m,
            25m,
            [],
            kind is HaciendaXmlDocumentKind.ReciboElectronicoPago
                or HaciendaXmlDocumentKind.FacturaElectronicaCompra
                ? [
                    new ParsedHaciendaReference(
                        "01",
                        "50601102600310115777600100001010000000001123456789",
                        new DateTimeOffset(
                            2026, 10, 1, 11, 12, 56, TimeSpan.FromHours(-6)),
                        "01",
                        "Referencia fiscal de prueba")
                ]
                : [],
            new ParsedTotals(
                0, 0, 0, 0,
                25, 0, 0, 0,
                25, 0, 0, 0,
                25, 0, 25, 0,
                0, 0, 25),
            []);
}