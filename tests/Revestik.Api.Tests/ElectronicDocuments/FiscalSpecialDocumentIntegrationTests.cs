using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Revestik.Api.Configuration;
using Revestik.Api.Data;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Api.Services.HaciendaXml;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class FiscalSpecialDocumentIntegrationTests
{
    [Theory]
    [InlineData(HaciendaXmlDocumentKind.NotaCreditoElectronica, ElectronicDocumentType.CreditNote)]
    [InlineData(HaciendaXmlDocumentKind.NotaDebitoElectronica, ElectronicDocumentType.DebitNote)]
    public async Task Notes_PreserveMultipleReferencesWithoutEconomicPostings(
        HaciendaXmlDocumentKind kind, ElectronicDocumentType expected)
    {
        await using var db = NewDb();
        await SeedUser(db);
        var references = Enumerable.Range(1, 10)
            .Select(i => Reference(i)).ToArray();
        var document = Document(kind, references: references);

        await Service(db, document).ImportAsync([1, 2, 3], "tester", default);

        var saved = await db.ElectronicDocuments
            .Include(x => x.References).SingleAsync();
        Assert.Equal(expected, saved.DocumentType);
        Assert.Equal(ElectronicDocumentDirection.Received, saved.Direction);
        Assert.Equal(ElectronicDocumentAdjustmentStatus.PendingReview, saved.AdjustmentStatus);
        Assert.Null(saved.PurchaseId);
        Assert.Null(saved.CategoryId);
        Assert.Equal(10, saved.References.Count);
        Assert.Equal(Enumerable.Range(1, 10), saved.References.OrderBy(x => x.Sequence).Select(x => x.Sequence));
        Assert.All(saved.References, x => Assert.Null(x.RelatedElectronicDocumentId));
        Assert.Empty(db.Purchases);
        Assert.Empty(db.Expenses);
        Assert.Empty(db.PurchasePayments);
        Assert.Empty(db.InventoryMovements);
    }

    [Theory]
    [InlineData(HaciendaXmlDocumentKind.NotaCreditoElectronica)]
    [InlineData(HaciendaXmlDocumentKind.NotaDebitoElectronica)]
    public async Task Notes_RejectMoreThanTenReferencesWithoutPersistence(HaciendaXmlDocumentKind kind)
    {
        await using var db = NewDb();
        await SeedUser(db);
        var document = Document(kind, references: Enumerable.Range(1, 11).Select(Reference).ToArray());
        await Assert.ThrowsAsync<ElectronicDocumentXmlException>(() =>
            Service(db, document).ImportAsync([1], "tester", default));
        Assert.Empty(db.ElectronicDocuments);
    }

    [Theory]
    [InlineData(HaciendaXmlDocumentKind.TiqueteElectronico, ElectronicDocumentType.ElectronicTicket)]
    [InlineData(HaciendaXmlDocumentKind.FacturaElectronicaCompra, ElectronicDocumentType.PurchaseInvoice)]
    [InlineData(HaciendaXmlDocumentKind.FacturaElectronicaExportacion, ElectronicDocumentType.ExportInvoice)]
    [InlineData(HaciendaXmlDocumentKind.ReciboElectronicoPago, ElectronicDocumentType.ElectronicPaymentReceipt)]
    public async Task SpecialDocuments_AreEvidenceNotNewEconomicTransactions(
        HaciendaXmlDocumentKind kind, ElectronicDocumentType expected)
    {
        await using var db = NewDb();
        await SeedUser(db);
        var document = Document(kind);
        await Service(db, document).ImportAsync([2, 3], "tester", default);

        var saved = await db.ElectronicDocuments.SingleAsync();
        Assert.Equal(expected, saved.DocumentType);
        Assert.Equal(ElectronicDocumentDirection.Received, saved.Direction);
        Assert.Equal(ElectronicDocumentProcessingStatus.Pending, saved.ProcessingStatus);
        Assert.Null(saved.PurchaseId);
        Assert.Null(saved.CategoryId);
        Assert.Empty(db.Purchases);
        Assert.Empty(db.Expenses);
        Assert.Empty(db.PurchasePayments);
        Assert.Empty(db.InventoryMovements);
    }

    [Theory]
    [InlineData(HaciendaXmlDocumentKind.NotaCreditoElectronica)]
    [InlineData(HaciendaXmlDocumentKind.TiqueteElectronico)]
    [InlineData(HaciendaXmlDocumentKind.FacturaElectronicaCompra)]
    [InlineData(HaciendaXmlDocumentKind.ReciboElectronicoPago)]
    public async Task SpecialDocuments_RejectUnrelatedRecipient(HaciendaXmlDocumentKind kind)
    {
        await using var db = NewDb();
        await SeedUser(db);
        var document = Document(kind) with
        {
            Receiver = new ParsedHaciendaParty("Otra empresa", "02", "3101999999", "", "")
        };
        await Assert.ThrowsAsync<InvalidElectronicDocumentReceiverException>(() =>
            Service(db, document).ImportAsync([4], "tester", default));
        Assert.Empty(db.ElectronicDocuments);
    }

    private static RevestikDbContext NewDb() => new(
        new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"FiscalSpecial-{Guid.NewGuid():N}").Options);

    private static async Task SeedUser(RevestikDbContext db)
    {
        db.Users.Add(new ApplicationUser
        {
            Id = "tester", UserName = "tester@example.com",
            NormalizedUserName = "TESTER@EXAMPLE.COM",
            Email = "tester@example.com", NormalizedEmail = "TESTER@EXAMPLE.COM",
            DisplayName = "Tester", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static ElectronicDocumentImportService Service(RevestikDbContext db, ParsedHaciendaDocument document) =>
        new(db, new FixedParser(document), Options.Create(new CompanyOptions
        {
            TaxIdentificationType = "02", TaxIdentificationNumber = "3102959852"
        }));

    private sealed class FixedParser(ParsedHaciendaDocument document) : IElectronicDocumentXmlParser
    {
        public ParsedReceivedXml Parse(ReadOnlyMemory<byte> xml) => document;
    }

    private static ParsedHaciendaReference Reference(int number) => new(
        "01", $"50601102600310115777600100001010000000001{number:00000000}",
        new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.FromHours(-6)),
        "01", $"Referencia de prueba {number}");

    private static ParsedHaciendaDocument Document(
        HaciendaXmlDocumentKind kind, IReadOnlyList<ParsedHaciendaReference>? references = null) => new(
        kind,
        "50602102600310115777600500001010000051684120639723",
        "00500001010000051684",
        new DateTimeOffset(2026, 10, 2, 11, 12, 56, TimeSpan.FromHours(-6)),
        new ParsedHaciendaParty("Proveedor", "02", "3101157776", "", ""),
        new ParsedHaciendaParty("Revestik", "02", "3102959852", "", ""),
        "", "", "01", null, "CRC", 1m, 25m, [],
        references ?? [Reference(1)],
        new ParsedTotals(0, 0, 0, 0, 25, 0, 0, 0, 25, 0, 0, 0, 25, 0, 25, 0, 0, 0, 25),
        []);
}
