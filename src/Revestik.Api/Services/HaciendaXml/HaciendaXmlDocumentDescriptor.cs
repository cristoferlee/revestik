namespace Revestik.Api.Services.HaciendaXml;

public sealed record HaciendaXmlDocumentDescriptor(
    HaciendaXmlDocumentKind Kind,
    string RootElement,
    string Namespace,
    string SchemaFileName,
    bool ProcessingEnabled);
