namespace Revestik.Shared.ElectronicDocuments;

public sealed record ElectronicDocumentImportResponse(
    string Kind,
    int Id,
    string Clave,
    string? DocumentType,
    int? ElectronicDocumentId);
