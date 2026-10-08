using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Revestik.Api.Configuration;
using Revestik.Api.Data;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Api.Services.HaciendaXml;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class ReceivedDocumentInboxFiscalAcceptanceTests
{
    [Theory]
    [InlineData(HaciendaXmlDocumentKind.NotaCreditoElectronica)]
    [InlineData(HaciendaXmlDocumentKind.NotaDebitoElectronica)]
    [InlineData(HaciendaXmlDocumentKind.TiqueteElectronico)]
    [InlineData(HaciendaXmlDocumentKind.FacturaElectronicaCompra)]
    [InlineData(HaciendaXmlDocumentKind.FacturaElectronicaExportacion)]
    [InlineData(HaciendaXmlDocumentKind.ReciboElectronicoPago)]
    public async Task Stage_SupportedFiscalType_IsPendingReview(HaciendaXmlDocumentKind kind)
    {
        await using var fixture = new Fixture(kind);
        var item = await fixture.Inbox.StageAsync([1, 2, 3], "fiscal.xml", "Upload", default);
        Assert.Equal(ReceivedDocumentInboxStatus.PendingReview, item.Status);
        Assert.Equal(kind.ToString(), item.DocumentKind);
        Assert.Equal(25m, item.Total);
        Assert.Empty(fixture.Db.ElectronicDocuments);
        Assert.Empty(fixture.Db.Purchases);
        Assert.Empty(fixture.Db.Expenses);
    }

    [Fact]
    public async Task Accept_Receipt_ImportsOnlyFiscalEvidence()
    {
        await using var fixture = new Fixture(HaciendaXmlDocumentKind.ReciboElectronicoPago);
        fixture.Db.Users.Add(new ApplicationUser
        {
            Id = "tester", UserName = "tester@example.com", NormalizedUserName = "TESTER@EXAMPLE.COM",
            Email = "tester@example.com", NormalizedEmail = "TESTER@EXAMPLE.COM",
            DisplayName = "Tester", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        });
        await fixture.Db.SaveChangesAsync();
        var staged = await fixture.Inbox.StageAsync([1, 2, 3], "fiscal.xml", "Upload", default);
        await fixture.Inbox.AcceptAsync(staged.Id, "tester", default);
        var saved = await fixture.Db.ElectronicDocuments.SingleAsync();
        Assert.Equal(ElectronicDocumentType.ElectronicPaymentReceipt, saved.DocumentType);
        Assert.Null(saved.PurchaseId);
        Assert.Null(saved.CategoryId);
        Assert.Empty(fixture.Db.Expenses);
        Assert.Empty(fixture.Db.Purchases);
        Assert.Empty(fixture.Db.PurchasePayments);
    }

    [Fact]
    public async Task Stage_DocumentAddressedToAnotherCompany_IsRejected()
    {
        await using var fixture = new Fixture(HaciendaXmlDocumentKind.FacturaElectronicaExportacion,
            receiver: new ParsedHaciendaParty("Other", "02", "3101999999", "", ""));
        await Assert.ThrowsAsync<ReceivedDocumentInboxException>(() =>
            fixture.Inbox.StageAsync([1], "other.xml", "Upload", default));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "revestik-inbox-46-" + Guid.NewGuid().ToString("N"));
        public RevestikDbContext Db { get; }
        public ReceivedDocumentInboxService Inbox { get; }

        public Fixture(HaciendaXmlDocumentKind kind, ParsedHaciendaParty? receiver = null)
        {
            Db = new RevestikDbContext(new DbContextOptionsBuilder<RevestikDbContext>()
                .UseInMemoryDatabase("Inbox46-" + Guid.NewGuid()).Options);
            receiver ??= new ParsedHaciendaParty("Revestik", "02", "3102959852", "", "");
            var reference = new ParsedHaciendaReference("01",
                "50601102600310115777600100001010000000001123456789",
                new DateTimeOffset(2026, 10, 1, 11, 12, 56, TimeSpan.FromHours(-6)), "01", "Test");
            var document = new ParsedHaciendaDocument(
                kind, "50602102600310115777600500001010000051684120639723",
                "00500001010000051684", new DateTimeOffset(2026, 10, 2, 11, 12, 56, TimeSpan.FromHours(-6)),
                new ParsedHaciendaParty("Supplier", "02", "3101157776", "", ""), receiver,
                "", "", "01", null, "CRC", 1m, 25m, [],
                [reference], new ParsedTotals(0, 0, 0, 0, 25, 0, 0, 0, 25, 0, 0, 0, 25, 0, 25, 0, 0, 0, 25), []);
            var parser = new StubParser(document);
            var company = Options.Create(new CompanyOptions
                { TaxIdentificationType = "02", TaxIdentificationNumber = "3102959852" });
            var descriptor = HaciendaXmlDocumentCatalog.All.Single(x => x.Kind == kind);
            var schema = new StubSchema(descriptor);
            Inbox = new ReceivedDocumentInboxService(Db, parser, schema,
                new ElectronicDocumentImportService(Db, parser, company), company,
                Options.Create(new ReceivedDocumentInboxOptions { StoragePath = directory }),
                new EphemeralDataProtectionProvider(),
                new StubWebHostEnvironment(directory));
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private sealed class StubParser(ParsedHaciendaDocument document) : IElectronicDocumentXmlParser
    {
        public ParsedReceivedXml Parse(ReadOnlyMemory<byte> xml) => document;
    }

    private sealed class StubSchema(HaciendaXmlDocumentDescriptor descriptor) : IHaciendaXmlSchemaValidator
    {
        public HaciendaXmlValidationResult Validate(ReadOnlyMemory<byte> xml) => new(descriptor);
    }

    private sealed class StubWebHostEnvironment(string path) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Revestik.Api.Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = path;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = path;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
