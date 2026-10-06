using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Revestik.Api.Configuration;
using Revestik.Api.Data;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class ElectronicDocumentImportServiceTests
{
    [Fact]
    public async Task ImportInvoice_CreatesActiveSupplierAndPendingDocument()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var service = CreateService(db);

        var result = await service.ImportAsync(
            Encoding.UTF8.GetBytes(InvoiceXml), UserId, CancellationToken.None);

        var document = await db.ElectronicDocuments.Include(x => x.Lines).SingleAsync();
        var supplier = await db.Suppliers.SingleAsync();
        Assert.Equal("ElectronicDocument", result.Kind);
        Assert.True(supplier.IsActive);
        Assert.Equal("3101157776", supplier.IdentificationNumber);
        Assert.Equal("Pending", document.ProcessingStatus.ToString());
        Assert.Equal(28635.01134m, document.TotalDocument);
        Assert.Single(document.Lines);
        Assert.NotEmpty(document.OriginalXml);
    }

    [Fact]
    public async Task ImportHaciendaResponseBeforeInvoice_LinksWhenInvoiceArrives()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var service = CreateService(db);

        await service.ImportAsync(Encoding.UTF8.GetBytes(HaciendaXml), UserId, CancellationToken.None);
        Assert.Null((await db.HaciendaResponses.SingleAsync()).ElectronicDocumentId);

        await service.ImportAsync(Encoding.UTF8.GetBytes(InvoiceXml), UserId, CancellationToken.None);
        Assert.NotNull((await db.HaciendaResponses.SingleAsync()).ElectronicDocumentId);
    }

    [Fact]
    public async Task ImportInvoice_WithWrongReceiver_RejectsWithoutPersistence()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var service = CreateService(db);
        var xml = InvoiceXml.Replace("3102959852", "3100000000", StringComparison.Ordinal);

        await Assert.ThrowsAsync<InvalidElectronicDocumentReceiverException>(() =>
            service.ImportAsync(Encoding.UTF8.GetBytes(xml), UserId, CancellationToken.None));

        Assert.Empty(db.ElectronicDocuments);
        Assert.Empty(db.Suppliers);
    }

    private const string UserId = "xml-import-user";

    private static RevestikDbContext CreateDb() => new(
        new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"ElectronicDocuments-{Guid.NewGuid()}").Options);

    private static ElectronicDocumentImportService CreateService(RevestikDbContext db) => new(
        db,
        new ElectronicDocumentXmlParser(),
        Options.Create(new CompanyOptions
        {
            TaxIdentificationType = "02",
            TaxIdentificationNumber = "3102959852"
        }));

    private static async Task SeedUserAsync(RevestikDbContext db)
    {
        db.Users.Add(new ApplicationUser
        {
            Id = UserId,
            UserName = "xml@example.com",
            NormalizedUserName = "XML@EXAMPLE.COM",
            Email = "xml@example.com",
            NormalizedEmail = "XML@EXAMPLE.COM",
            EmailConfirmed = true,
            DisplayName = "XML Import User",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private const string InvoiceXml = """
<FacturaElectronica xmlns="https://cdn.comprobanteselectronicos.go.cr/xml-schemas/v4.4/facturaElectronica"><Clave>50602102600310115777600500001010000051684120639723</Clave><NumeroConsecutivo>00500001010000051684</NumeroConsecutivo><FechaEmision>2026-10-02T11:12:56-06:00</FechaEmision><Emisor><Nombre>EXPOCERAMICA ACABADOS SOCIEDAD ANONIMA</Nombre><Identificacion><Tipo>02</Tipo><Numero>3101157776</Numero></Identificacion><NombreComercial>EXPOCERAMICA</NombreComercial><Telefono><NumTelefono>22033004</NumTelefono></Telefono><CorreoElectronico>facturaclientes@expoceramicacr.com</CorreoElectronico></Emisor><Receptor><Nombre>3-102-959852 SOCIEDAD DE RESPONSABILIDAD LIMITADA</Nombre><Identificacion><Tipo>02</Tipo><Numero>3102959852</Numero></Identificacion></Receptor><CondicionVenta>01</CondicionVenta><DetalleServicio><LineaDetalle><NumeroLinea>1</NumeroLinea><CodigoCABYS>3737000000100</CodigoCABYS><Cantidad>7.800</Cantidad><UnidadMedida>Unid</UnidadMedida><Detalle>PISO</Detalle><PrecioUnitario>3248.81000</PrecioUnitario><MontoTotal>25340.71800</MontoTotal><SubTotal>25340.71800</SubTotal><BaseImponible>25340.71800</BaseImponible><Impuesto><Codigo>01</Codigo><CodigoTarifaIVA>08</CodigoTarifaIVA><Tarifa>13.00</Tarifa><Monto>3294.29334</Monto></Impuesto><ImpuestoNeto>3294.29334</ImpuestoNeto><MontoTotalLinea>28635.01134</MontoTotalLinea></LineaDetalle></DetalleServicio><ResumenFactura><CodigoTipoMoneda><CodigoMoneda>CRC</CodigoMoneda><TipoCambio>1.00000</TipoCambio></CodigoTipoMoneda><TotalMercanciasGravadas>25340.71800</TotalMercanciasGravadas><TotalGravado>25340.71800</TotalGravado><TotalVenta>25340.71800</TotalVenta><TotalVentaNeta>25340.71800</TotalVentaNeta><TotalImpuesto>3294.29334</TotalImpuesto><TotalComprobante>28635.01134</TotalComprobante></ResumenFactura></FacturaElectronica>
""";

    private const string HaciendaXml = """
<MensajeHacienda xmlns="https://cdn.comprobanteselectronicos.go.cr/xml-schemas/v4.4/mensajeHacienda"><Clave>50602102600310115777600500001010000051684120639723</Clave><NombreEmisor>EXPOCERAMICA ACABADOS SOCIEDAD ANONIMA</NombreEmisor><TipoIdentificacionEmisor>02</TipoIdentificacionEmisor><NumeroCedulaEmisor>3101157776</NumeroCedulaEmisor><NombreReceptor>3-102-959852 SOCIEDAD DE RESPONSABILIDAD LIMITADA</NombreReceptor><TipoIdentificacionReceptor>02</TipoIdentificacionReceptor><NumeroCedulaReceptor>3102959852</NumeroCedulaReceptor><Mensaje>1</Mensaje><EstadoMensaje>Aceptado</EstadoMensaje><DetalleMensaje>.</DetalleMensaje><MontoTotalImpuesto>3294.29334</MontoTotalImpuesto><TotalFactura>28635.01134</TotalFactura></MensajeHacienda>
""";
}