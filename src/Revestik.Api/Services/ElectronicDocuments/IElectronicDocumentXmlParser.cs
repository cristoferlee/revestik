namespace Revestik.Api.Services.ElectronicDocuments;

public interface IElectronicDocumentXmlParser
{
    ParsedReceivedXml Parse(ReadOnlyMemory<byte> xml);
}
