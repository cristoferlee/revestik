using System.Xml;

namespace Revestik.Api.Services.HaciendaXml;

public static class HaciendaXmlDocumentDetector
{
    private const long MaxCharactersInDocument = 10_485_760;

    public static HaciendaXmlDocumentDescriptor Detect(ReadOnlyMemory<byte> xml)
    {
        if (xml.IsEmpty)
            throw new HaciendaXmlValidationException("El archivo XML está vacío.");

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
                    IgnoreComments = true,
                    IgnoreWhitespace = true
                });

            reader.MoveToContent();

            if (!HaciendaXmlDocumentCatalog.TryResolve(reader.LocalName, reader.NamespaceURI, out var descriptor))
            {
                throw new HaciendaXmlValidationException(
                    $"El documento no corresponde a una estructura Hacienda v4.4 reconocida: {{{reader.NamespaceURI}}}{reader.LocalName}.");
            }

            return descriptor;
        }
        catch (HaciendaXmlValidationException)
        {
            throw;
        }
        catch (XmlException exception)
        {
            throw new HaciendaXmlValidationException(
                $"El XML está malformado o contiene una construcción no permitida: {exception.Message}");
        }
    }
}
