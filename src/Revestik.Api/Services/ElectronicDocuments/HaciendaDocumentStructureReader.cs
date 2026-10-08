using System.Globalization;
using System.Xml.Linq;
using Revestik.Api.Services.HaciendaXml;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.ElectronicDocuments;

internal static class HaciendaDocumentStructureReader
{
    public static ParsedHaciendaDocument Read(XElement root, HaciendaXmlDocumentDescriptor descriptor)
    {
        var ns = root.Name.Namespace;
        var summary = Required(root, ns + "ResumenFactura");
        var currency = summary.Element(ns + "CodigoTipoMoneda");
        var detail = root.Element(ns + "DetalleServicio");

        var lines = detail?.Elements(ns + "LineaDetalle")
            .Select(line => new ParsedHaciendaLine(
                int.Parse(ValueRequired(line, ns + "NumeroLinea"), CultureInfo.InvariantCulture),
                Value(line, ns + "CodigoCABYS"),
                Value(line, ns + "Detalle"),
                DecimalOptional(line, ns + "Cantidad"),
                DecimalOptional(line, ns + "PrecioUnitario"),
                DecimalOptional(line, ns + "MontoTotalLinea")))
            .ToArray() ?? [];

        var references = root.Elements(ns + "InformacionReferencia")
            .Select(item => new ParsedHaciendaReference(
                ValueRequired(item, ns + "TipoDocIR"),
                ValueRequired(item, ns + "Numero"),
                DateTimeOffset.Parse(ValueRequired(item, ns + "FechaEmisionIR"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Value(item, ns + "Codigo"),
                Value(item, ns + "Razon")))
            .ToArray();

        var requiresFiscalBreakdown = descriptor.Kind is HaciendaXmlDocumentKind.NotaCreditoElectronica
            or HaciendaXmlDocumentKind.NotaDebitoElectronica
            or HaciendaXmlDocumentKind.TiqueteElectronico
            or HaciendaXmlDocumentKind.FacturaElectronicaCompra
            or HaciendaXmlDocumentKind.FacturaElectronicaExportacion
            or HaciendaXmlDocumentKind.ReciboElectronicoPago;
        var fullFiscalSummary = summary.Element(ns + "TotalVenta") is not null &&
            summary.Element(ns + "TotalVentaNeta") is not null;
        var fullFiscalLines = detail?.Elements(ns + "LineaDetalle").All(item =>
            item.Element(ns + "Cantidad") is not null &&
            item.Element(ns + "UnidadMedida") is not null &&
            item.Element(ns + "Detalle") is not null &&
            item.Element(ns + "PrecioUnitario") is not null &&
            item.Element(ns + "MontoTotal") is not null &&
            item.Element(ns + "SubTotal") is not null &&
            item.Element(ns + "MontoTotalLinea") is not null) ?? true;
        var fiscalLines = requiresFiscalBreakdown && fullFiscalLines
            ? detail?.Elements(ns + "LineaDetalle").Select(item => ReadFiscalLine(item, ns)).ToArray() ?? []
            : null;
        var fiscalTotals = requiresFiscalBreakdown && fullFiscalSummary ? ReadFiscalTotals(summary, ns) : null;

        return new ParsedHaciendaDocument(
            descriptor.Kind,
            ValueRequired(root, ns + "Clave"),
            ValueRequired(root, ns + "NumeroConsecutivo"),
            DateTimeOffset.Parse(ValueRequired(root, ns + "FechaEmision"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            ReadParty(Required(root, ns + "Emisor"), ns),
            root.Element(ns + "Receptor") is { } receiver ? ReadParty(receiver, ns) : null,
            Value(root, ns + "CodigoActividadEmisor"),
            Value(root, ns + "CodigoActividadReceptor"),
            Value(root, ns + "CondicionVenta"),
            IntOptional(root, ns + "PlazoCredito"),
            currency is null ? string.Empty : Value(currency, ns + "CodigoMoneda"),
            currency is null ? null : DecimalOptional(currency, ns + "TipoCambio"),
            DecimalRequired(summary, ns + "TotalComprobante"),
            lines,
            references,
            fiscalTotals,
            fiscalLines);
    }


    private static ParsedLine ReadFiscalLine(XElement line, XNamespace ns)
    {
        var commercialCode = line.Element(ns + "CodigoComercial");
        var discounts = line.Elements(ns + "Descuento")
            .Select(item => new ParsedDiscount(
                DecimalRequired(item, ns + "MontoDescuento"),
                Value(item, ns + "CodigoDescuento"),
                Value(item, ns + "NaturalezaDescuento")))
            .ToList();
        var taxes = line.Elements(ns + "Impuesto")
            .Select(item => new ParsedTax(
                ValueRequired(item, ns + "Codigo"),
                Value(item, ns + "CodigoTarifaIVA"),
                DecimalOptional(item, ns + "Tarifa") ?? 0m,
                DecimalRequired(item, ns + "Monto")))
            .ToList();

        return new ParsedLine(
            int.Parse(ValueRequired(line, ns + "NumeroLinea"), CultureInfo.InvariantCulture),
            Value(line, ns + "CodigoCABYS"),
            commercialCode is null ? string.Empty : Value(commercialCode, ns + "Tipo"),
            commercialCode is null ? string.Empty : Value(commercialCode, ns + "Codigo"),
            DecimalRequired(line, ns + "Cantidad"),
            ValueRequired(line, ns + "UnidadMedida"),
            Value(line, ns + "UnidadMedidaComercial"),
            ValueRequired(line, ns + "Detalle"),
            DecimalRequired(line, ns + "PrecioUnitario"),
            DecimalRequired(line, ns + "MontoTotal"),
            DecimalRequired(line, ns + "SubTotal"),
            DecimalOptional(line, ns + "BaseImponible") ?? 0m,
            DecimalOptional(line, ns + "ImpuestoNeto") ?? 0m,
            DecimalRequired(line, ns + "MontoTotalLinea"),
            discounts,
            taxes);
    }

    private static ParsedTotals ReadFiscalTotals(XElement summary, XNamespace ns) => new(
        DecimalOptional(summary, ns + "TotalServGravados") ?? 0m,
        DecimalOptional(summary, ns + "TotalServExentos") ?? 0m,
        DecimalOptional(summary, ns + "TotalServExonerado") ?? 0m,
        DecimalOptional(summary, ns + "TotalServNoSujeto") ?? 0m,
        DecimalOptional(summary, ns + "TotalMercanciasGravadas") ?? 0m,
        DecimalOptional(summary, ns + "TotalMercanciasExentas") ?? 0m,
        DecimalOptional(summary, ns + "TotalMercExonerada") ?? 0m,
        DecimalOptional(summary, ns + "TotalMercNoSujeta") ?? 0m,
        DecimalOptional(summary, ns + "TotalGravado") ?? 0m,
        DecimalOptional(summary, ns + "TotalExento") ?? 0m,
        DecimalOptional(summary, ns + "TotalExonerado") ?? 0m,
        DecimalOptional(summary, ns + "TotalNoSujeto") ?? 0m,
        DecimalRequired(summary, ns + "TotalVenta"),
        DecimalOptional(summary, ns + "TotalDescuentos") ?? 0m,
        DecimalRequired(summary, ns + "TotalVentaNeta"),
        DecimalOptional(summary, ns + "TotalImpuesto") ?? 0m,
        DecimalOptional(summary, ns + "TotalIVADevuelto") ?? 0m,
        DecimalOptional(summary, ns + "TotalOtrosCargos") ?? 0m,
        DecimalRequired(summary, ns + "TotalComprobante"));

    private static ParsedHaciendaParty ReadParty(XElement party, XNamespace ns)
    {
        var identification = party.Element(ns + "Identificacion");
        return new ParsedHaciendaParty(
            ValueRequired(party, ns + "Nombre"),
            identification is null ? string.Empty : Value(identification, ns + "Tipo"),
            identification is null ? string.Empty : Value(identification, ns + "Numero"),
            Value(party, ns + "NombreComercial"),
            Value(party, ns + "CorreoElectronico"));
    }

    private static XElement Required(XElement parent, XName name) =>
        parent.Element(name) ?? throw new ElectronicDocumentXmlException($"Falta el elemento obligatorio {name.LocalName}.");

    private static string Value(XElement parent, XName name) => parent.Element(name)?.Value.Trim() ?? string.Empty;

    private static string ValueRequired(XElement parent, XName name)
    {
        var value = Value(parent, name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new ElectronicDocumentXmlException($"Falta el valor obligatorio {name.LocalName}.")
            : value;
    }

    private static decimal DecimalRequired(XElement parent, XName name) =>
        decimal.Parse(ValueRequired(parent, name), NumberStyles.Number, CultureInfo.InvariantCulture);

    private static decimal? DecimalOptional(XElement parent, XName name)
    {
        var value = Value(parent, name);
        return string.IsNullOrWhiteSpace(value) ? null : decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
    }

    private static int? IntOptional(XElement parent, XName name)
    {
        var value = Value(parent, name);
        return string.IsNullOrWhiteSpace(value) ? null : int.Parse(value, CultureInfo.InvariantCulture);
    }
}
