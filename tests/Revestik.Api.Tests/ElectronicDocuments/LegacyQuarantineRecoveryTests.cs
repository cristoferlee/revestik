using System.Text.Json.Nodes;
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

public sealed class LegacyQuarantineRecoveryTests
{
    [Theory]
    [InlineData(HaciendaXmlDocumentKind.NotaCreditoElectronica, ElectronicDocumentType.CreditNote)]
    [InlineData(HaciendaXmlDocumentKind.NotaDebitoElectronica, ElectronicDocumentType.DebitNote)]
    [InlineData(HaciendaXmlDocumentKind.TiqueteElectronico, ElectronicDocumentType.ElectronicTicket)]
    public async Task LegacySupportedDocument_CanBeAcceptedAfterRevalidation(
        HaciendaXmlDocumentKind kind, ElectronicDocumentType expected)
    {
        await using var f = new Fixture(kind);
        var staged = await f.Inbox.StageAsync([1, 2, 3], "legacy.xml", "Gmail:test", default);
        await f.MarkLegacyAsync(staged.Id);
        var accepted = await f.Inbox.AcceptAsync(staged.Id, "tester", default);
        var document = await f.Db.ElectronicDocuments.SingleAsync();
        Assert.Equal(expected, document.DocumentType);
        Assert.Equal(accepted.Import.ElectronicDocumentId, document.Id);
        Assert.Null(document.PurchaseId);
        Assert.Empty(f.Db.Purchases);
        Assert.Empty(f.Db.PurchasePayments);
        Assert.Empty(f.Db.Expenses);
        Assert.Empty(f.Db.InventoryMovements);
        Assert.Empty(await f.Inbox.GetPendingAsync(default));
    }

    [Fact]
    public async Task Duplicate_RejectOnlyRemovesQuarantineCopy()
    {
        await using var f = new Fixture(HaciendaXmlDocumentKind.TiqueteElectronico);
        var first = await f.Inbox.StageAsync([1], "first.xml", "Gmail:test", default);
        await f.Inbox.AcceptAsync(first.Id, "tester", default);
        var duplicate = await f.Inbox.StageAsync([1], "second.xml", "Gmail:test", default);
        Assert.Equal(ReceivedDocumentInboxStatus.Duplicate, duplicate.Status);
        Assert.True(await f.Inbox.RejectAsync(duplicate.Id, default));
        Assert.Empty(await f.Inbox.GetPendingAsync(default));
        Assert.Single(f.Db.ElectronicDocuments);
        Assert.Empty(f.Db.Purchases);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "revestik-legacy-" + Guid.NewGuid().ToString("N"));
        private readonly EphemeralDataProtectionProvider provider = new();
        public RevestikDbContext Db { get; }
        public ReceivedDocumentInboxService Inbox { get; }
        public Fixture(HaciendaXmlDocumentKind kind)
        {
            Db = new RevestikDbContext(new DbContextOptionsBuilder<RevestikDbContext>()
                .UseInMemoryDatabase("LegacyQuarantine-" + Guid.NewGuid()).Options);
            Db.Users.Add(new ApplicationUser
            {
                Id = "tester", UserName = "tester@example.com", NormalizedUserName = "TESTER@EXAMPLE.COM",
                Email = "tester@example.com", NormalizedEmail = "TESTER@EXAMPLE.COM",
                DisplayName = "Tester", IsActive = true, CreatedAtUtc = DateTime.UtcNow
            });
            Db.SaveChanges();
            var reference = new ParsedHaciendaReference("01",
                "50601102600310115777600100001010000000001123456789",
                new DateTimeOffset(2026, 10, 1, 11, 12, 56, TimeSpan.FromHours(-6)), "01", "Prueba");
            var parsed = new ParsedHaciendaDocument(kind,
                "50602102600310115777600500001010000051684120639723", "00500001010000051684",
                new DateTimeOffset(2026, 10, 2, 11, 12, 56, TimeSpan.FromHours(-6)),
                new ParsedHaciendaParty("Proveedor", "02", "3101157776", "", ""),
                new ParsedHaciendaParty("Revestik", "02", "3102959852", "", ""),
                "", "", "01", null, "CRC", 1m, 25m, [], [reference],
                new ParsedTotals(0, 0, 0, 0, 25, 0, 0, 0, 25, 0, 0, 0, 25, 0, 25, 0, 0, 0, 25), []);
            var parser = new StubParser(parsed);
            var company = Options.Create(new CompanyOptions
            { TaxIdentificationType = "02", TaxIdentificationNumber = "3102959852" });
            var validator = new StubValidator(HaciendaXmlDocumentCatalog.All.Single(x => x.Kind == kind));
            Inbox = new ReceivedDocumentInboxService(Db, parser, validator,
                new ElectronicDocumentImportService(Db, parser, company), company,
                Options.Create(new ReceivedDocumentInboxOptions { StoragePath = folder }),
                provider, new Env(folder));
        }
        public async Task MarkLegacyAsync(Guid id)
        {
            var file = Path.Combine(folder, $"{id:N}.inbox");
            var protector = provider.CreateProtector("Revestik.ReceivedDocuments.Inbox.v1");
            var value = JsonNode.Parse(protector.Unprotect(await File.ReadAllTextAsync(file)))!;
            value["Status"] = (int)ReceivedDocumentInboxStatus.RecognizedNotEnabled;
            await File.WriteAllTextAsync(file, protector.Protect(value.ToJsonString()));
        }
        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }
    private sealed class StubParser(ParsedHaciendaDocument parsed) : IElectronicDocumentXmlParser
    {
        public ParsedReceivedXml Parse(ReadOnlyMemory<byte> xml) => parsed;
    }
    private sealed class StubValidator(HaciendaXmlDocumentDescriptor descriptor) : IHaciendaXmlSchemaValidator
    {
        public HaciendaXmlValidationResult Validate(ReadOnlyMemory<byte> xml) => new(descriptor);
    }
    private sealed class Env(string path) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Revestik.Api.Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = path;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = path;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
