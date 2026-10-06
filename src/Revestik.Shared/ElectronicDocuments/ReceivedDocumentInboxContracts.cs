namespace Revestik.Shared.ElectronicDocuments;

public enum ReceivedDocumentInboxStatus
{
    PendingReview = 1,
    Duplicate = 2,
    Accepted = 3,
    Rejected = 4,
    RecognizedNotEnabled = 5
}

public sealed record ReceivedDocumentInboxItemResponse(
    Guid Id,
    string Source,
    string FileName,
    DateTime ReceivedAtUtc,
    ReceivedDocumentInboxStatus Status,
    string DocumentKind,
    string Clave,
    string IssuerName,
    string IssuerIdentification,
    string ReceiverName,
    string CurrencyCode,
    decimal Total,
    decimal TotalCrcEquivalent,
    bool RequiresHighAmountReview,
    IReadOnlyList<string> Warnings,
    int? ExistingElectronicDocumentId,
    int? AcceptedElectronicDocumentId);

public sealed record ReceivedDocumentInboxListResponse(
    IReadOnlyList<ReceivedDocumentInboxItemResponse> Items);

public sealed record ReceivedDocumentInboxAcceptResponse(
    Guid InboxId,
    ElectronicDocumentImportResponse Import);
