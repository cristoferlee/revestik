namespace Revestik.Api.Services.HaciendaXml;

public interface IHaciendaXmlSchemaValidator
{
    HaciendaXmlValidationResult Validate(ReadOnlyMemory<byte> xml);
}
