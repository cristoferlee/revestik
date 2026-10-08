using System.Globalization;
using System.Xml.Linq;
using Revestik.Api.Services.HaciendaXml;

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
            references);
    }

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
