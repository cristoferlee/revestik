using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Revestik.Api.Configuration;
using Revestik.Api.Data;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class ElectronicDocumentNoteImportTests
{
    private const string UserId = "note-import-user";
    private const string CompanyNumber = "3102959852";
    private const string OtherNumber = "3101671274";

    [Theory]
    [InlineData("NotaCreditoElectronica", "notaCreditoElectronica", ElectronicDocumentType.CreditNote)]
    [InlineData("NotaDebitoElectronica", "notaDebitoElectronica", ElectronicDocumentType.DebitNote)]
    public async Task ReceivedNote_PersistsFiscalAmountsWithoutEconomicEffects(
        string root, string ns, ElectronicDocumentType expectedType)
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var service = CreateService(db);
        var bytes = Encoding.UTF8.GetBytes(BuildXml(root, ns, OtherNumber, CompanyNumber));

        await service.ImportAsync(bytes, UserId, CancellationToken.None);

        var note = await db.ElectronicDocuments.Include(x => x.Lines)
            .ThenInclude(x => x.Taxes).SingleAsync();
        Assert.Equal(expectedType, note.DocumentType);
        Assert.Equal(ElectronicDocumentDirection.Received, note.Direction);
        Assert.Equal(ElectronicDocumentAdjustmentStatus.PendingReview, note.AdjustmentStatus);
        Assert.Equal(ElectronicDocumentProcessingStatus.Pending, note.ProcessingStatus);
        Assert.Equal(113m, note.TotalDocument);
        Assert.Equal(13m, note.TotalTax);
        Assert.Equal(100m, note.TotalNetSale);
        Assert.Equal(13m, Assert.Single(Assert.Single(note.Lines).Taxes).Amount);
        Assert.Equal(bytes, note.OriginalXml);
        Assert.Null(note.PurchaseId);
        Assert.Null(note.SupplierId);
        Assert.Null(note.CategoryId);
        Assert.Empty(db.Suppliers);
        Assert.Empty(db.Purchases);
        Assert.Empty(db.Expenses);
        var reference = Assert.Single(note.References);
        Assert.Equal(1, reference.Sequence);
        Assert.Equal("01", reference.ReferencedDocumentTypeCode);
        Assert.Equal("01", reference.ReferenceCode);
        Assert.Equal("Ajuste", reference.Reason);
        Assert.Null(reference.RelatedElectronicDocumentId);
    }

    [Fact]
    public async Task IssuedNoteWithoutReceiver_IsStoredAsIssued()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var service = CreateService(db);
        await service.ImportAsync(Encoding.UTF8.GetBytes(BuildXml(
            "NotaCreditoElectronica", "notaCreditoElectronica", CompanyNumber, null)),
            UserId, CancellationToken.None);
        var note = await db.ElectronicDocuments.SingleAsync();
        Assert.Equal(ElectronicDocumentDirection.Issued, note.Direction);
        Assert.Equal(ElectronicDocumentAdjustmentStatus.PendingReview, note.AdjustmentStatus);
        Assert.Empty(db.Suppliers);
    }

    [Fact]
    public async Task NoteNotBelongingToCompany_IsRejected()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var service = CreateService(db);
        await Assert.ThrowsAsync<InvalidElectronicDocumentReceiverException>(() =>
            service.ImportAsync(Encoding.UTF8.GetBytes(BuildXml(
                "NotaCreditoElectronica", "notaCreditoElectronica", OtherNumber, "3101000000")),
                UserId, CancellationToken.None));
        Assert.Empty(db.ElectronicDocuments);
    }

    [Fact]
    public async Task DuplicateFiscalKey_IsRejected()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var service = CreateService(db);
        var xml = Encoding.UTF8.GetBytes(BuildXml(
            "NotaCreditoElectronica", "notaCreditoElectronica", OtherNumber, CompanyNumber));
        await service.ImportAsync(xml, UserId, CancellationToken.None);
        await Assert.ThrowsAsync<DuplicateElectronicDocumentException>(() =>
            service.ImportAsync(xml, UserId, CancellationToken.None));
        Assert.Single(db.ElectronicDocuments);
    }

    [Fact]
    public async Task IncompleteFiscalBreakdown_IsRejectedWithoutPersistence()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var service = CreateService(db);
        var xml = BuildXml("NotaDebitoElectronica", "notaDebitoElectronica", OtherNumber, CompanyNumber)
            .Replace("<SubTotal>100</SubTotal>", string.Empty, StringComparison.Ordinal);
        await Assert.ThrowsAsync<ElectronicDocumentXmlException>(() =>
            service.ImportAsync(Encoding.UTF8.GetBytes(xml), UserId, CancellationToken.None));
        Assert.Empty(db.ElectronicDocuments);
    }

    [Fact]
    public async Task MultipleReferences_PersistInOriginalOrderWithoutLinkingOrPosting()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var xml = BuildXml("NotaCreditoElectronica", "notaCreditoElectronica", OtherNumber, CompanyNumber);
        const string firstReference = "<InformacionReferencia><TipoDocIR>01</TipoDocIR><Numero>50602102600310167127400100001010010477709195577801</Numero><FechaEmisionIR>2026-10-01T09:00:00-06:00</FechaEmisionIR><Codigo>01</Codigo><Razon>Ajuste</Razon></InformacionReferencia>";
        var secondReference = firstReference.Replace("<Numero>50602102600310167127400100001010010477709195577801</Numero>",
                "<Numero>50602102600310167127400100001010010477709195577802</Numero>", StringComparison.Ordinal)
            .Replace("<Razon>Ajuste</Razon>", "<Razon>Segunda referencia</Razon>", StringComparison.Ordinal);
        xml = xml.Replace(firstReference, firstReference + secondReference, StringComparison.Ordinal);

        await CreateService(db).ImportAsync(Encoding.UTF8.GetBytes(xml), UserId, CancellationToken.None);

        var document = await db.ElectronicDocuments.Include(x => x.References).SingleAsync();
        var references = document.References.OrderBy(x => x.Sequence).ToArray();
        Assert.Equal(2, references.Length);
        Assert.Equal(new[] { 1, 2 }, references.Select(x => x.Sequence));
        Assert.EndsWith("01", references[0].ReferenceNumber);
        Assert.EndsWith("02", references[1].ReferenceNumber);
        Assert.Equal("Segunda referencia", references[1].Reason);
        Assert.All(references, x => Assert.Null(x.RelatedElectronicDocumentId));
        Assert.Equal(ElectronicDocumentAdjustmentStatus.PendingReview, document.AdjustmentStatus);
        Assert.Empty(db.Purchases);
        Assert.Empty(db.Expenses);
    }

    [Fact]
    public async Task MissingReference_RejectsEntireImport()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var xml = BuildXml("NotaDebitoElectronica", "notaDebitoElectronica", OtherNumber, CompanyNumber);
        var beginning = xml.IndexOf("<InformacionReferencia>", StringComparison.Ordinal);
        var end = xml.IndexOf("</InformacionReferencia>", StringComparison.Ordinal)
            + "</InformacionReferencia>".Length;
        xml = xml.Remove(beginning, end - beginning);

        await Assert.ThrowsAsync<ElectronicDocumentXmlException>(() =>
            CreateService(db).ImportAsync(Encoding.UTF8.GetBytes(xml), UserId, CancellationToken.None));
        Assert.Empty(db.ElectronicDocuments);
        Assert.Empty(db.Set<Revestik.Api.Models.ElectronicDocumentReference>());
    }

    [Fact]
    public async Task DuplicateImport_DoesNotDuplicateReferences()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var service = CreateService(db);
        var xml = Encoding.UTF8.GetBytes(BuildXml(
            "NotaCreditoElectronica", "notaCreditoElectronica", OtherNumber, CompanyNumber));
        await service.ImportAsync(xml, UserId, CancellationToken.None);
        await Assert.ThrowsAsync<DuplicateElectronicDocumentException>(() =>
            service.ImportAsync(xml, UserId, CancellationToken.None));

        Assert.Single(db.ElectronicDocuments);
        Assert.Single(db.Set<Revestik.Api.Models.ElectronicDocumentReference>());
    }

    private static RevestikDbContext CreateDb() => new(
        new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"FiscalNotes-{Guid.NewGuid()}").Options);

    private static ElectronicDocumentImportService CreateService(RevestikDbContext db) => new(
        db, new ElectronicDocumentXmlParser(), Options.Create(new CompanyOptions
        {
            TaxIdentificationType = "02",
            TaxIdentificationNumber = CompanyNumber
        }));

    private static async Task SeedUserAsync(RevestikDbContext db)
    {
        db.Users.Add(new ApplicationUser
        {
            Id = UserId,
            UserName = "note@example.com",
            NormalizedUserName = "NOTE@EXAMPLE.COM",
            Email = "note@example.com",
            NormalizedEmail = "NOTE@EXAMPLE.COM",
            EmailConfirmed = true,
            DisplayName = "Note Import User",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static string BuildXml(string root, string ns, string issuer, string? receiver)
    {
        var receiverXml = receiver is null ? "" :
            $"<Receptor><Nombre>Receptor</Nombre><Identificacion><Tipo>02</Tipo><Numero>{receiver}</Numero></Identificacion></Receptor>";
        return $"""
            <{root} xmlns="https://cdn.comprobanteselectronicos.go.cr/xml-schemas/v4.4/{ns}">
              <Clave>50602102600310167127400100001010010477709195577801</Clave>
              <NumeroConsecutivo>00100001010010477709</NumeroConsecutivo>
              <FechaEmision>2026-10-02T12:16:00-06:00</FechaEmision>
              <Emisor><Nombre>Emisor</Nombre><Identificacion><Tipo>02</Tipo><Numero>{issuer}</Numero></Identificacion></Emisor>
              {receiverXml}
              <DetalleServicio><LineaDetalle>
                <NumeroLinea>1</NumeroLinea><CodigoCABYS>3639001020000</CodigoCABYS><Cantidad>1</Cantidad>
                <UnidadMedida>Unid</UnidadMedida><Detalle>Servicio</Detalle><PrecioUnitario>105</PrecioUnitario>
                <MontoTotal>105</MontoTotal><Descuento><MontoDescuento>5</MontoDescuento><CodigoDescuento>01</CodigoDescuento><NaturalezaDescuento>Descuento</NaturalezaDescuento></Descuento>
                <SubTotal>100</SubTotal><Impuesto><Codigo>01</Codigo><CodigoTarifaIVA>08</CodigoTarifaIVA><Tarifa>13</Tarifa><Monto>13</Monto></Impuesto>
                <ImpuestoNeto>13</ImpuestoNeto><MontoTotalLinea>113</MontoTotalLinea>
              </LineaDetalle></DetalleServicio>
              <InformacionReferencia><TipoDocIR>01</TipoDocIR><Numero>50602102600310167127400100001010010477709195577801</Numero><FechaEmisionIR>2026-10-01T09:00:00-06:00</FechaEmisionIR><Codigo>01</Codigo><Razon>Ajuste</Razon></InformacionReferencia>
              <ResumenFactura><CodigoTipoMoneda><CodigoMoneda>CRC</CodigoMoneda><TipoCambio>1</TipoCambio></CodigoTipoMoneda>
                <TotalVenta>105</TotalVenta><TotalDescuentos>5</TotalDescuentos><TotalVentaNeta>100</TotalVentaNeta><TotalImpuesto>13</TotalImpuesto><TotalComprobante>113</TotalComprobante>
              </ResumenFactura>
            </{root}>
            """;
    }
}
