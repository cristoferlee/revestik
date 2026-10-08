using System.Text;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Api.Services.HaciendaXml;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class HaciendaNoteFiscalParsingTests
{
    private readonly ElectronicDocumentXmlParser parser = new();

    [Theory]
    [InlineData("NotaCreditoElectronica", "notaCreditoElectronica", HaciendaXmlDocumentKind.NotaCreditoElectronica)]
    [InlineData("NotaDebitoElectronica", "notaDebitoElectronica", HaciendaXmlDocumentKind.NotaDebitoElectronica)]
    public void Parse_Note_PreservesFiscalAmountsAndReferences(
        string root, string namespaceSuffix, HaciendaXmlDocumentKind kind)
    {
        var document = Assert.IsType<ParsedHaciendaDocument>(parser.Parse(
            Encoding.UTF8.GetBytes(BuildXml(root, namespaceSuffix))));

        Assert.Equal(kind, document.Kind);
        Assert.NotNull(document.FiscalTotals);
        Assert.Equal(113m, document.FiscalTotals.TotalDocument);
        Assert.Equal(100m, document.FiscalTotals.TotalNetSale);
        Assert.Equal(13m, document.FiscalTotals.TotalTax);
        var line = Assert.Single(document.FiscalLines!);
        Assert.Equal(113m, line.TotalLine);
        Assert.Equal(5m, Assert.Single(line.Discounts).Amount);
        Assert.Equal(13m, Assert.Single(line.Taxes).Amount);
        Assert.Equal("01", Assert.Single(document.References).DocumentType);
    }

    [Fact]
    public void Parse_NoteWithIncompleteLine_DoesNotMarkFiscalLinesAsComplete()
    {
        var xml = BuildXml("NotaCreditoElectronica", "notaCreditoElectronica")
            .Replace("<SubTotal>100</SubTotal>", string.Empty, StringComparison.Ordinal);

        var parsed = Assert.IsType<ParsedHaciendaDocument>(parser.Parse(Encoding.UTF8.GetBytes(xml)));
        Assert.Null(parsed.FiscalLines);
    }

    private static string BuildXml(string root, string namespaceSuffix) => $"""
        <{root} xmlns="https://cdn.comprobanteselectronicos.go.cr/xml-schemas/v4.4/{namespaceSuffix}">
          <Clave>50602102600310167127400100001010010477709195577801</Clave>
          <NumeroConsecutivo>00100001010010477709</NumeroConsecutivo>
          <FechaEmision>2026-10-02T12:16:00-06:00</FechaEmision>
          <Emisor><Nombre>Proveedor</Nombre><Identificacion><Tipo>02</Tipo><Numero>3101671274</Numero></Identificacion></Emisor>
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
