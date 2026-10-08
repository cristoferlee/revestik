using System.Text;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Api.Services.HaciendaXml;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class HaciendaDocumentStructureReaderTests
{
    private readonly ElectronicDocumentXmlParser parser = new();

    [Theory]
    [InlineData("TiqueteElectronico", "tiqueteElectronico", HaciendaXmlDocumentKind.TiqueteElectronico, false)]
    [InlineData("NotaCreditoElectronica", "notaCreditoElectronica", HaciendaXmlDocumentKind.NotaCreditoElectronica, true)]
    [InlineData("NotaDebitoElectronica", "notaDebitoElectronica", HaciendaXmlDocumentKind.NotaDebitoElectronica, true)]
    [InlineData("FacturaElectronicaCompra", "facturaElectronicaCompra", HaciendaXmlDocumentKind.FacturaElectronicaCompra, true)]
    [InlineData("FacturaElectronicaExportacion", "facturaElectronicaExportacion", HaciendaXmlDocumentKind.FacturaElectronicaExportacion, false)]
    [InlineData("ReciboElectronicoPago", "reciboElectronicoPago", HaciendaXmlDocumentKind.ReciboElectronicoPago, true)]
    public void Parse_RecognizedDocument_PreservesCommonFieldsAndReferences(
        string root, string namespaceSuffix, HaciendaXmlDocumentKind kind, bool hasReference)
    {
        var reference = hasReference
            ? "<InformacionReferencia><TipoDocIR>01</TipoDocIR><Numero>50602102600310167127400100001010010477709195577801</Numero><FechaEmisionIR>2026-10-01T09:00:00-06:00</FechaEmisionIR><Codigo>01</Codigo><Razon>Prueba</Razon></InformacionReferencia>"
            : string.Empty;
        var xml = $"""
            <{root} xmlns="https://cdn.comprobanteselectronicos.go.cr/xml-schemas/v4.4/{namespaceSuffix}">
                <Clave>50602102600310167127400100001010010477709195577801</Clave>
                <NumeroConsecutivo>00100001010010477709</NumeroConsecutivo>
                <FechaEmision>2026-10-02T12:16:00-06:00</FechaEmision>
                <Emisor><Nombre>Proveedor</Nombre><Identificacion><Tipo>02</Tipo><Numero>3101671274</Numero></Identificacion></Emisor>
                <CondicionVenta>01</CondicionVenta>
                <DetalleServicio><LineaDetalle><NumeroLinea>1</NumeroLinea><CodigoCABYS>3639001020000</CodigoCABYS><Detalle>Servicio</Detalle><Cantidad>1</Cantidad><PrecioUnitario>100</PrecioUnitario><MontoTotalLinea>113</MontoTotalLinea></LineaDetalle></DetalleServicio>
                {reference}
                <ResumenFactura><CodigoTipoMoneda><CodigoMoneda>CRC</CodigoMoneda><TipoCambio>1</TipoCambio></CodigoTipoMoneda><TotalComprobante>113</TotalComprobante></ResumenFactura>
            </{root}>
            """;

        var document = Assert.IsType<ParsedHaciendaDocument>(parser.Parse(Encoding.UTF8.GetBytes(xml)));

        Assert.Equal(kind, document.Kind);
        Assert.Null(document.Receiver);
        Assert.Equal("Proveedor", document.Issuer.Name);
        Assert.Equal(113m, document.TotalDocument);
        Assert.Single(document.Lines);
        Assert.Equal(hasReference ? 1 : 0, document.References.Count);
        if (hasReference)
            Assert.Equal("01", document.References[0].DocumentType);
    }

    [Fact]
    public void Parse_NoteWithMultipleReferences_PreservesAllReferences()
    {
        const string ns = "https://cdn.comprobanteselectronicos.go.cr/xml-schemas/v4.4/notaCreditoElectronica";
        var xml = $"""
            <NotaCreditoElectronica xmlns="{ns}">
              <Clave>50602102600310167127400100001010010477709195577801</Clave>
              <NumeroConsecutivo>00100001010010477709</NumeroConsecutivo>
              <FechaEmision>2026-10-02T12:16:00-06:00</FechaEmision>
              <Emisor><Nombre>Proveedor</Nombre></Emisor>
              <InformacionReferencia><TipoDocIR>01</TipoDocIR><Numero>ABC</Numero><FechaEmisionIR>2026-10-01T09:00:00-06:00</FechaEmisionIR><Codigo>01</Codigo><Razon>Ajuste A</Razon></InformacionReferencia>
              <InformacionReferencia><TipoDocIR>04</TipoDocIR><Numero>DEF</Numero><FechaEmisionIR>2026-10-01T09:00:00-06:00</FechaEmisionIR><Codigo>02</Codigo><Razon>Ajuste B</Razon></InformacionReferencia>
              <ResumenFactura><TotalComprobante>12.50</TotalComprobante></ResumenFactura>
            </NotaCreditoElectronica>
            """;
        var document = Assert.IsType<ParsedHaciendaDocument>(parser.Parse(Encoding.UTF8.GetBytes(xml)));
        Assert.Equal(2, document.References.Count);
        Assert.Equal("ABC", document.References[0].Number);
        Assert.Equal("DEF", document.References[1].Number);
        Assert.Empty(document.Lines);
    }
}
