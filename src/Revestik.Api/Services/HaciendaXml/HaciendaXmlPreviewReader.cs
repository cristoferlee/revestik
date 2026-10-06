using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Revestik.Api.Services.HaciendaXml;

public sealed record HaciendaXmlPreview(
    string Clave,
    string IssuerName,
    string IssuerIdentification,
    string ReceiverName,
    string CurrencyCode,
    decimal Total);

public static class HaciendaXmlPreviewReader
{
    public static HaciendaXmlPreview Read(ReadOnlyMemory<byte> xml)
    {
        using var stream = new MemoryStream(xml.ToArray(), writable: false);
        using var reader = XmlReader.Create(
            stream,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 10_485_760
            });

        var document = XDocument.Load(reader);
        var root = document.Root ?? throw new HaciendaXmlValidationException("El XML no contiene raíz.");

        string First(params string[] names) =>
            root.DescendantsAndSelf()
                .FirstOrDefault(element => names.Contains(element.Name.LocalName, StringComparer.Ordinal))
                ?.Value.Trim() ?? string.Empty;

        string PartyIdentification(string partyName, string fallbackName)
        {
            var party = root.DescendantsAndSelf()
                .FirstOrDefault(element => element.Name.LocalName == partyName);
            var id = party?.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "Numero")
                ?.Value.Trim();
            return string.IsNullOrWhiteSpace(id) ? First(fallbackName) : id;
        }

        var totalText = First("TotalComprobante", "TotalFactura", "MontoTotal");
        _ = decimal.TryParse(totalText, NumberStyles.Number, CultureInfo.InvariantCulture, out var total);

        return new HaciendaXmlPreview(
            First("Clave"),
            First("NombreEmisor", "Nombre"),
            PartyIdentification("Emisor", "NumeroCedulaEmisor"),
            First("NombreReceptor"),
            First("CodigoMoneda"),
            total);
    }
}
