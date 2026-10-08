using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Revestik.Api.Configuration;
using Revestik.Api.Data;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class ElectronicTicketImportTests
{
    [Fact]
    public async Task TicketWithoutReceiver_IsStoredUnclassifiedAndWithoutExpense()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var original = Encoding.UTF8.GetBytes(TicketXml);
        var service = CreateService(db);

        await service.ImportAsync(original, "ticket-user", CancellationToken.None);

        var stored = await db.ElectronicDocuments.Include(x => x.Lines).SingleAsync();
        Assert.Equal(ElectronicDocumentType.ElectronicTicket, stored.DocumentType);
        Assert.Equal(ElectronicDocumentDirection.Received, stored.Direction);
        Assert.Equal(ElectronicDocumentProcessingStatus.Pending, stored.ProcessingStatus);
        Assert.Null(stored.AdjustmentStatus);
        Assert.Null(stored.CategoryId);
        Assert.Null(stored.PurchaseId);
        Assert.Null(stored.SupplierId);
        Assert.Equal(113m, stored.TotalDocument);
        Assert.Equal(13m, stored.TotalTax);
        Assert.Equal(original, stored.OriginalXml);
        Assert.Empty(db.Expenses);
        Assert.Empty(db.Purchases);
        Assert.Empty(db.Suppliers);
    }

    [Fact]
    public async Task TicketDuplicateClave_IsRejected()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var service = CreateService(db);
        var data = Encoding.UTF8.GetBytes(TicketXml);
        await service.ImportAsync(data, "ticket-user", CancellationToken.None);
        await Assert.ThrowsAsync<DuplicateElectronicDocumentException>(() =>
            service.ImportAsync(data, "ticket-user", CancellationToken.None));
        Assert.Single(db.ElectronicDocuments);
    }

    [Fact]
    public async Task TicketExplicitlyAddressedToAnotherReceiver_IsRejected()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var xml = TicketXml.Replace("</Emisor>",
            "</Emisor><Receptor><Nombre>Otra empresa</Nombre><Identificacion><Tipo>02</Tipo><Numero>3101999999</Numero></Identificacion></Receptor>",
            StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidElectronicDocumentReceiverException>(() =>
            CreateService(db).ImportAsync(Encoding.UTF8.GetBytes(xml), "ticket-user", CancellationToken.None));
        Assert.Empty(db.ElectronicDocuments);
    }

    private static RevestikDbContext CreateDb() => new(
        new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"TicketImport-{Guid.NewGuid()}").Options);

    private static ElectronicDocumentImportService CreateService(RevestikDbContext db) => new(
        db, new ElectronicDocumentXmlParser(), Options.Create(new CompanyOptions
        {
            TaxIdentificationType = "02", TaxIdentificationNumber = "3102959852"
        }));

    private static async Task SeedUserAsync(RevestikDbContext db)
    {
        db.Users.Add(new ApplicationUser
        {
            Id = "ticket-user", UserName = "ticket@example.com", NormalizedUserName = "TICKET@EXAMPLE.COM",
            Email = "ticket@example.com", NormalizedEmail = "TICKET@EXAMPLE.COM",
            DisplayName = "Ticket Import Test", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private const string TicketXml = """
<TiqueteElectronico xmlns="https://cdn.comprobanteselectronicos.go.cr/xml-schemas/v4.4/tiqueteElectronico"><Clave>50602102600310167127400100004010000000001123456789</Clave><NumeroConsecutivo>00100004010000000001</NumeroConsecutivo><FechaEmision>2026-10-02T11:12:56-06:00</FechaEmision><Emisor><Nombre>Comercio ejemplo</Nombre><Identificacion><Tipo>02</Tipo><Numero>3101671274</Numero></Identificacion></Emisor><CondicionVenta>01</CondicionVenta><DetalleServicio><LineaDetalle><NumeroLinea>1</NumeroLinea><CodigoCABYS>3737000000100</CodigoCABYS><Cantidad>1</Cantidad><UnidadMedida>Unid</UnidadMedida><Detalle>Servicio de prueba</Detalle><PrecioUnitario>100</PrecioUnitario><MontoTotal>100</MontoTotal><SubTotal>100</SubTotal><Impuesto><Codigo>01</Codigo><CodigoTarifaIVA>08</CodigoTarifaIVA><Tarifa>13</Tarifa><Monto>13</Monto></Impuesto><ImpuestoNeto>13</ImpuestoNeto><MontoTotalLinea>113</MontoTotalLinea></LineaDetalle></DetalleServicio><ResumenFactura><CodigoTipoMoneda><CodigoMoneda>CRC</CodigoMoneda><TipoCambio>1</TipoCambio></CodigoTipoMoneda><TotalVenta>100</TotalVenta><TotalVentaNeta>100</TotalVentaNeta><TotalImpuesto>13</TotalImpuesto><TotalComprobante>113</TotalComprobante></ResumenFactura></TiqueteElectronico>
""";
}
