namespace Revestik.Shared.ElectronicDocuments;

public sealed record ElectronicDocumentListItemResponse(
    int Id,
    string Clave,
    ElectronicDocumentType DocumentType,
    string NumeroConsecutivo,
    DateTimeOffset FechaEmision,
    string IssuerName,
    string IssuerIdentification,
    string CurrencyCode,
    decimal TotalDocument,
    ElectronicDocumentProcessingStatus ProcessingStatus,
    int? CategoryId,
    string? CategoryName,
    int? SupplierId,
    int? PurchaseId,
    bool HasHaciendaResponse);

public sealed record ElectronicDocumentListResponse(
    IReadOnlyList<ElectronicDocumentListItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
