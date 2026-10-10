using System.Text;
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

/// <summary>
/// Runs against the actual pinned v4.4 XSDs, without a schema or parser stub.
/// The optional positive case uses a real, locally selected fiscal XML in an isolated
/// in-memory database and temporary protected inbox; it never writes to production.
/// </summary>
public sealed class HaciendaRealPipelineIntegrationTests
{
    private const string CompanyId = "3102959852";
    private const string UserId = "hacienda-integration-user";

    [Theory]
    [InlineData(HaciendaXmlDocumentKind.FacturaElectronica)]
    [InlineData(HaciendaXmlDocumentKind.NotaCreditoElectronica)]
    [InlineData(HaciendaXmlDocumentKind.NotaDebitoElectronica)]
    [InlineData(HaciendaXmlDocumentKind.TiqueteElectronico)]
    [InlineData(HaciendaXmlDocumentKind.FacturaElectronicaCompra)]
    [InlineData(HaciendaXmlDocumentKind.FacturaElectronicaExportacion)]
    [InlineData(HaciendaXmlDocumentKind.ReciboElectronicoPago)]
    public void RealXsd_RejectsDocumentMissingMandatoryFields(HaciendaXmlDocumentKind kind)
    {
        var descriptor = HaciendaXmlDocumentCatalog.All.Single(x => x.Kind == kind);
        var xml = Encoding.UTF8.GetBytes(
            $"<{descriptor.RootElement} xmlns=\"{descriptor.Namespace}\" />");

        var validator = new HaciendaXmlSchemaValidator(new TestEnvironment(ApiRoot()));
        Assert.Throws<HaciendaXmlValidationException>(() => validator.Validate(xml));
    }

    [Fact]
    public async Task LocalFiscalXml_RealXsdToInboxAcceptance_StoresEvidenceWithoutEconomicEffects()
    {
        // Opt-in: point to ONE valid received XML from the actual Hacienda/Gmail workflow.
        // Do not commit or copy private fiscal XML to this repository.
        var fixturePath = Environment.GetEnvironmentVariable("REVESTIK_HACIENDA_TEST_XML");
        if (string.IsNullOrWhiteSpace(fixturePath))
            return; // Explicit local opt-in; the seven real-XSD negative cases always run.

        Assert.True(File.Exists(fixturePath),
            "REVESTIK_HACIENDA_TEST_XML points to a missing file.");
        var xml = await File.ReadAllBytesAsync(fixturePath);
        var schemaValidator = new HaciendaXmlSchemaValidator(new TestEnvironment(ApiRoot()));
        var validated = schemaValidator.Validate(xml);
        Assert.NotEqual(HaciendaXmlDocumentKind.MensajeReceptor, validated.Descriptor.Kind);

        var parser = new ElectronicDocumentXmlParser();
        var parsed = parser.Parse(xml);
        // The positive test intentionally covers RECEIVED evidence only, with an
        // explicit recipient. No issued or recipient-less case is assumed here.
        var fiscalReceiver = parsed switch
        {
            ParsedReceivedElectronicDocument invoice => invoice.Document.Receiver.Identification,
            ParsedHaciendaDocument fiscal => fiscal.Receiver?.Identification,
            _ => null
        };
        Assert.Equal(CompanyId, fiscalReceiver);

        await using var db = new RevestikDbContext(
            new DbContextOptionsBuilder<RevestikDbContext>()
                .UseInMemoryDatabase("HaciendaRealPipeline-" + Guid.NewGuid().ToString("N"))
                .Options);
        db.Users.Add(new ApplicationUser
        {
            Id = UserId,
            UserName = "fiscal-integration@example.invalid",
            NormalizedUserName = "FISCAL-INTEGRATION@EXAMPLE.INVALID",
            Email = "fiscal-integration@example.invalid",
            NormalizedEmail = "FISCAL-INTEGRATION@EXAMPLE.INVALID",
            DisplayName = "Fiscal Integration Test",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var directory = Path.Combine(Path.GetTempPath(),
            "revestik-hacienda-integration-" + Guid.NewGuid().ToString("N"));
        try
        {
            var company = Options.Create(new CompanyOptions
            {
                TaxIdentificationType = "02",
                TaxIdentificationNumber = CompanyId
            });
            var importer = new ElectronicDocumentImportService(db, parser, company);
            var inbox = new ReceivedDocumentInboxService(
                db, parser, schemaValidator, importer, company,
                Options.Create(new ReceivedDocumentInboxOptions { StoragePath = directory }),
                new EphemeralDataProtectionProvider(), new TestEnvironment(ApiRoot()));

            var staged = await inbox.StageAsync(xml, "real-fiscal.xml", "IntegrationTest", default);
            Assert.Equal(ReceivedDocumentInboxStatus.PendingReview, staged.Status);
            Assert.Empty(db.ElectronicDocuments);
            Assert.Empty(db.Purchases);
            Assert.Empty(db.Expenses);
            Assert.Empty(db.PurchasePayments);
            Assert.Empty(db.InventoryMovements);

            await inbox.AcceptAsync(staged.Id, UserId, default);
            var saved = await db.ElectronicDocuments.SingleAsync();
            Assert.Equal(xml, saved.OriginalXml);
            Assert.Null(saved.PurchaseId);
            Assert.Equal(ElectronicDocumentProcessingStatus.Pending, saved.ProcessingStatus);
            Assert.Empty(db.Purchases);
            Assert.Empty(db.Expenses);
            Assert.Empty(db.PurchasePayments);
            Assert.Empty(db.InventoryMovements);
            Assert.Empty(db.InventoryCostLayers);

            var duplicate = await inbox.StageAsync(xml, "duplicate.xml", "IntegrationTest", default);
            Assert.Equal(ReceivedDocumentInboxStatus.Duplicate, duplicate.Status);
            Assert.Single(db.ElectronicDocuments);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static string ApiRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "src", "Revestik.Api");
            if (File.Exists(Path.Combine(candidate, "Data", "Hacienda", "v4.4",
                    "FacturaElectronica_V4.4.xsd")))
                return candidate;
            current = current.Parent;
        }
        throw new InvalidOperationException(
            "No se encontraron los XSD locales del proyecto Revestik.Api.");
    }

    private sealed class TestEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Revestik.Api.Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
