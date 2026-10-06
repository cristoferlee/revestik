using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.ElectronicDocuments;

public sealed class ElectronicDocumentXmlParser : IElectronicDocumentXmlParser
{
    private static readonly XNamespace InvoiceNamespace =
        "https://cdn.comprobanteselectronicos.go.cr/xml-schemas/v4.4/facturaElectronica";

    private static readonly XNamespace HaciendaMessageNamespace =
        "https://cdn.comprobanteselectronicos.go.cr/xml-schemas/v4.4/mensajeHacienda";

    private const long MaxCharactersInDocument = 10_485_760;

    public ParsedReceivedXml Parse(ReadOnlyMemory<byte> xml)
    {
        if (xml.IsEmpty)
        {
            throw new ElectronicDocumentXmlException("El archivo XML está vacío.");
        }

        try
        {
            using var stream = new MemoryStream(xml.ToArray(), writable: false);
            using var reader = XmlReader.Create(
                stream,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = MaxCharactersInDocument,
                    IgnoreComments = false,
                    IgnoreWhitespace = false
                });

            var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            var root = document.Root ?? throw new ElectronicDocumentXmlException("El XML no contiene un elemento raíz.");

            if (root.Name == InvoiceNamespace + "FacturaElectronica")
            {
                return new ParsedReceivedElectronicDocument(ParseInvoice(root));
            }

            if (root.Name == HaciendaMessageNamespace + "MensajeHacienda")
            {
                return new ParsedReceivedHaciendaResponse(ParseHaciendaResponse(root));
            }

            throw new ElectronicDocumentXmlException(
                $"Tipo o namespace XML no soportado: {root.Name}.");
        }
        catch (ElectronicDocumentXmlException)
        {
            throw;
        }
        catch (XmlException exception)
        {
            throw new ElectronicDocumentXmlException("El archivo XML está malformado o contiene una construcción XML no permitida.", exception);
        }
        catch (FormatException exception)
        {
            throw new ElectronicDocumentXmlException("El XML contiene un valor numérico o de fecha con formato inválido.", exception);
        }
    }

    private static ParsedElectronicDocument ParseInvoice(XElement root)
    {
        var ns = root.Name.Namespace;
        var issuerElement = RequiredElement(root, ns + "Emisor");
        var receiverElement = RequiredElement(root, ns + "Receptor");
        var detailElement = RequiredElement(root, ns + "DetalleServicio");
        var summaryElement = RequiredElement(root, ns + "ResumenFactura");

        var lines = detailElement.Elements(ns + "LineaDetalle")
            .Select(line => ParseLine(line, ns))
            .ToList();

        if (lines.Count == 0)
        {
            throw new ElectronicDocumentXmlException("La factura electrónica no contiene líneas de detalle.");
        }

        var currency = RequiredElement(summaryElement, ns + "CodigoTipoMoneda");

        return new ParsedElectronicDocument(
            ElectronicDocumentType.Invoice,
            RequiredValue(root, ns + "Clave", 50),
            RequiredValue(root, ns + "NumeroConsecutivo", 20),
            DateTimeOffset.Parse(RequiredValue(root, ns + "FechaEmision"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            OptionalValue(root, ns + "CodigoActividadEmisor"),
            OptionalValue(root, ns + "CodigoActividadReceptor"),
            ParseParty(issuerElement, ns),
            ParseParty(receiverElement, ns),
            RequiredValue(root, ns + "CondicionVenta"),
            OptionalInt(root, ns + "PlazoCredito"),
            RequiredValue(currency, ns + "CodigoMoneda"),
            DecimalValue(currency, ns + "TipoCambio", required: true),
            ParseTotals(summaryElement, ns),
            lines);
    }

    private static ParsedHaciendaResponse ParseHaciendaResponse(XElement root)
    {
        var ns = root.Name.Namespace;
        return new ParsedHaciendaResponse(
            RequiredValue(root, ns + "Clave", 50),
            RequiredValue(root, ns + "NombreEmisor"),
            RequiredValue(root, ns + "TipoIdentificacionEmisor"),
            RequiredValue(root, ns + "NumeroCedulaEmisor"),
            RequiredValue(root, ns + "NombreReceptor"),
            RequiredValue(root, ns + "TipoIdentificacionReceptor"),
            RequiredValue(root, ns + "NumeroCedulaReceptor"),
            RequiredValue(root, ns + "Mensaje"),
            RequiredValue(root, ns + "EstadoMensaje"),
            OptionalValue(root, ns + "DetalleMensaje"),
            DecimalValue(root, ns + "MontoTotalImpuesto"),
            DecimalValue(root, ns + "TotalFactura", required: true));
    }

    private static ParsedParty ParseParty(XElement element, XNamespace ns)
    {
        var identification = RequiredElement(element, ns + "Identificacion");
        var phone = element.Element(ns + "Telefono");
        var address = element.Element(ns + "Ubicacion");

        IEnumerable<string> addressParts = address is null
            ? Enumerable.Empty<string>()
            : new[]
            {
                OptionalValue(address, ns + "Provincia"),
                OptionalValue(address, ns + "Canton"),
                OptionalValue(address, ns + "Distrito"),
                OptionalValue(address, ns + "Barrio"),
                OptionalValue(address, ns + "OtrasSenas")
            }.Where(value => !string.IsNullOrWhiteSpace(value));

        return new ParsedParty(
            RequiredValue(element, ns + "Nombre"),
            OptionalValue(element, ns + "NombreComercial"),
            RequiredValue(identification, ns + "Tipo"),
            RequiredValue(identification, ns + "Numero"),
            phone is null ? string.Empty : OptionalValue(phone, ns + "NumTelefono"),
            OptionalValue(element, ns + "CorreoElectronico"),
            string.Join(" | ", addressParts));
    }

    private static ParsedLine ParseLine(XElement line, XNamespace ns)
    {
        var commercialCode = line.Element(ns + "CodigoComercial");

        var discounts = line.Elements(ns + "Descuento")
            .Select(item => new ParsedDiscount(
                DecimalValue(item, ns + "MontoDescuento", required: true),
                OptionalValue(item, ns + "CodigoDescuento"),
                OptionalValue(item, ns + "NaturalezaDescuento")))
            .ToList();

        var taxes = line.Elements(ns + "Impuesto")
            .Select(item => new ParsedTax(
                RequiredValue(item, ns + "Codigo"),
                OptionalValue(item, ns + "CodigoTarifaIVA"),
                DecimalValue(item, ns + "Tarifa"),
                DecimalValue(item, ns + "Monto", required: true)))
            .ToList();

        return new ParsedLine(
            IntValue(line, ns + "NumeroLinea", required: true),
            RequiredValue(line, ns + "CodigoCABYS", 13),
            commercialCode is null ? string.Empty : OptionalValue(commercialCode, ns + "Tipo"),
            commercialCode is null ? string.Empty : OptionalValue(commercialCode, ns + "Codigo"),
            DecimalValue(line, ns + "Cantidad", required: true),
            RequiredValue(line, ns + "UnidadMedida"),
            OptionalValue(line, ns + "UnidadMedidaComercial"),
            RequiredValue(line, ns + "Detalle"),
            DecimalValue(line, ns + "PrecioUnitario", required: true),
            DecimalValue(line, ns + "MontoTotal", required: true),
            DecimalValue(line, ns + "SubTotal", required: true),
            DecimalValue(line, ns + "BaseImponible"),
            DecimalValue(line, ns + "ImpuestoNeto"),
            DecimalValue(line, ns + "MontoTotalLinea", required: true),
            discounts,
            taxes);
    }

    private static ParsedTotals ParseTotals(XElement summary, XNamespace ns) => new(
        DecimalValue(summary, ns + "TotalServGravados"),
        DecimalValue(summary, ns + "TotalServExentos"),
        DecimalValue(summary, ns + "TotalServExonerado"),
        DecimalValue(summary, ns + "TotalServNoSujeto"),
        DecimalValue(summary, ns + "TotalMercanciasGravadas"),
        DecimalValue(summary, ns + "TotalMercanciasExentas"),
        DecimalValue(summary, ns + "TotalMercExonerada"),
        DecimalValue(summary, ns + "TotalMercNoSujeta"),
        DecimalValue(summary, ns + "TotalGravado"),
        DecimalValue(summary, ns + "TotalExento"),
        DecimalValue(summary, ns + "TotalExonerado"),
        DecimalValue(summary, ns + "TotalNoSujeto"),
        DecimalValue(summary, ns + "TotalVenta", required: true),
        DecimalValue(summary, ns + "TotalDescuentos"),
        DecimalValue(summary, ns + "TotalVentaNeta", required: true),
        DecimalValue(summary, ns + "TotalImpuesto"),
        DecimalValue(summary, ns + "TotalIVADevuelto"),
        DecimalValue(summary, ns + "TotalOtrosCargos"),
        DecimalValue(summary, ns + "TotalComprobante", required: true));

    private static XElement RequiredElement(XElement parent, XName name) =>
        parent.Element(name) ?? throw new ElectronicDocumentXmlException($"Falta el elemento obligatorio {name.LocalName}.");

    private static string RequiredValue(XElement parent, XName name, int? exactLength = null)
    {
        var value = OptionalValue(parent, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ElectronicDocumentXmlException($"Falta el valor obligatorio {name.LocalName}.");
        }

        if (exactLength.HasValue && value.Length != exactLength.Value)
        {
            throw new ElectronicDocumentXmlException($"{name.LocalName} debe contener exactamente {exactLength.Value} caracteres.");
        }

        return value;
    }

    private static string OptionalValue(XElement parent, XName name) =>
        parent.Element(name)?.Value.Trim() ?? string.Empty;

    private static decimal DecimalValue(XElement parent, XName name, bool required = false)
    {
        var value = OptionalValue(parent, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                throw new ElectronicDocumentXmlException($"Falta el valor obligatorio {name.LocalName}.");
            }
            return 0m;
        }

        return decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
    }

    private static int IntValue(XElement parent, XName name, bool required = false)
    {
        var value = OptionalValue(parent, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                throw new ElectronicDocumentXmlException($"Falta el valor obligatorio {name.LocalName}.");
            }
            return 0;
        }
        return int.Parse(value, CultureInfo.InvariantCulture);
    }

    private static int? OptionalInt(XElement parent, XName name)
    {
        var value = OptionalValue(parent, name);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : int.Parse(value, CultureInfo.InvariantCulture);
    }
}
