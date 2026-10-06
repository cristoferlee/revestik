using System.Text;
using Revestik.Api.Services.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class ElectronicDocumentXmlParserTests
{
    private readonly ElectronicDocumentXmlParser parser = new();

    [Fact]
    public void Parse_ValidInvoice_PreservesFiscalPrecisionAndBreakdown()
    {
        var parsed = Assert.IsType<ParsedReceivedElectronicDocument>(
            parser.Parse(Encoding.UTF8.GetBytes(InvoiceXml)));

        Assert.Equal("50602102600310167127400100001010010477709195577801", parsed.Document.Clave);
        Assert.Equal("3102959852", parsed.Document.Receiver.Identification);
        Assert.Equal("2220.9", parsed.Document.IssuerEconomicActivityCode);
        Assert.Equal("4759.0", parsed.Document.ReceiverEconomicActivityCode);
        Assert.Equal(49122.79500m, parsed.Document.Totals.TotalDocument);
        Assert.Equal(2, parsed.Document.Lines.Count);
        Assert.Equal(22238.40000m, parsed.Document.Lines[0].TotalLine);
        Assert.Equal(4320.00000m, parsed.Document.Lines[0].Discounts.Single().Amount);
        Assert.Equal(13.00m, parsed.Document.Lines[0].Taxes.Single().Rate);
    }

    [Fact]
    public void Parse_HaciendaResponse_ReturnsAuxiliaryResponse()
    {
        var parsed = Assert.IsType<ParsedReceivedHaciendaResponse>(
            parser.Parse(Encoding.UTF8.GetBytes(HaciendaXml)));

        Assert.Equal("Aceptado", parsed.Response.MessageStatus);
        Assert.Equal(49122.79500m, parsed.Response.TotalInvoice);
    }

    [Fact]
    public void Parse_Dtd_RejectsDocument()
    {
        const string xml = "<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///etc/passwd'>]><x>&e;</x>";
        Assert.Throws<ElectronicDocumentXmlException>(() =>
            parser.Parse(Encoding.UTF8.GetBytes(xml)));
    }

    private const string InvoiceXml = """
<?xml version="1.0" encoding="utf-8"?>
<FacturaElectronica xmlns="https://cdn.comprobanteselectronicos.go.cr/xml-schemas/v4.4/facturaElectronica">
<Clave>50602102600310167127400100001010010477709195577801</Clave><CodigoActividadEmisor>2220.9</CodigoActividadEmisor><CodigoActividadReceptor>4759.0</CodigoActividadReceptor><NumeroConsecutivo>00100001010010477709</NumeroConsecutivo><FechaEmision>2026-10-02T12:16:00-06:00</FechaEmision>
<Emisor><Nombre>Distribuidora Plastimex de Costa Rica S.A.</Nombre><Identificacion><Tipo>02</Tipo><Numero>3101671274</Numero></Identificacion><NombreComercial>Plastimex</NombreComercial><Telefono><NumTelefono>24539270</NumTelefono></Telefono><CorreoElectronico>ventas@plastimexsa.com</CorreoElectronico></Emisor>
<Receptor><Nombre>3-102-959852 S.A</Nombre><Identificacion><Tipo>02</Tipo><Numero>3102959852</Numero></Identificacion></Receptor><CondicionVenta>01</CondicionVenta>
<DetalleServicio>
<LineaDetalle><NumeroLinea>1</NumeroLinea><CodigoCABYS>3639001020000</CodigoCABYS><Cantidad>6.000</Cantidad><UnidadMedida>Unid</UnidadMedida><Detalle>Tablilla</Detalle><PrecioUnitario>4000.00000</PrecioUnitario><MontoTotal>24000.00000</MontoTotal><Descuento><MontoDescuento>4320.00000</MontoDescuento><CodigoDescuento>07</CodigoDescuento><NaturalezaDescuento>Descuento Comercial</NaturalezaDescuento></Descuento><SubTotal>19680.00000</SubTotal><BaseImponible>19680.00000</BaseImponible><Impuesto><Codigo>01</Codigo><CodigoTarifaIVA>08</CodigoTarifaIVA><Tarifa>13.00</Tarifa><Monto>2558.40000</Monto></Impuesto><ImpuestoNeto>2558.40000</ImpuestoNeto><MontoTotalLinea>22238.40000</MontoTotalLinea></LineaDetalle>
<LineaDetalle><NumeroLinea>2</NumeroLinea><CodigoCABYS>3631000029900</CodigoCABYS><Cantidad>2.000</Cantidad><UnidadMedida>Unid</UnidadMedida><Detalle>Lámina</Detalle><PrecioUnitario>13995.00000</PrecioUnitario><MontoTotal>27990.00000</MontoTotal><SubTotal>23791.50000</SubTotal><BaseImponible>23791.50000</BaseImponible><Impuesto><Codigo>01</Codigo><CodigoTarifaIVA>08</CodigoTarifaIVA><Tarifa>13.00</Tarifa><Monto>3092.89500</Monto></Impuesto><ImpuestoNeto>3092.89500</ImpuestoNeto><MontoTotalLinea>26884.39500</MontoTotalLinea></LineaDetalle>
</DetalleServicio>
<ResumenFactura><CodigoTipoMoneda><CodigoMoneda>CRC</CodigoMoneda><TipoCambio>1.00000</TipoCambio></CodigoTipoMoneda><TotalMercanciasGravadas>51990.00000</TotalMercanciasGravadas><TotalGravado>51990.00000</TotalGravado><TotalVenta>51990.00000</TotalVenta><TotalDescuentos>8518.50000</TotalDescuentos><TotalVentaNeta>43471.50000</TotalVentaNeta><TotalImpuesto>5651.29500</TotalImpuesto><TotalComprobante>49122.79500</TotalComprobante></ResumenFactura>
</FacturaElectronica>
""";

    private const string HaciendaXml = """
<?xml version="1.0" encoding="UTF-8"?>
<MensajeHacienda xmlns="https://cdn.comprobanteselectronicos.go.cr/xml-schemas/v4.4/mensajeHacienda"><Clave>50602102600310167127400100001010010477709195577801</Clave><NombreEmisor>Distribuidora Plastimex de Costa Rica S.A.</NombreEmisor><TipoIdentificacionEmisor>02</TipoIdentificacionEmisor><NumeroCedulaEmisor>3101671274</NumeroCedulaEmisor><NombreReceptor>3-102-959852 S.A</NombreReceptor><TipoIdentificacionReceptor>02</TipoIdentificacionReceptor><NumeroCedulaReceptor>3102959852</NumeroCedulaReceptor><Mensaje>1</Mensaje><EstadoMensaje>Aceptado</EstadoMensaje><DetalleMensaje>.</DetalleMensaje><MontoTotalImpuesto>5651.29500</MontoTotalImpuesto><TotalFactura>49122.79500</TotalFactura></MensajeHacienda>
""";
}
