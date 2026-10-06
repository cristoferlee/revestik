namespace Revestik.Shared.ElectronicDocuments;

public sealed record GmailIntegrationStatusResponse(
    bool IsConfigured,
    bool IsConnected,
    string ExpectedMailbox,
    string? ConnectedMailbox,
    DateTime? ConnectedAtUtc,
    DateTime? LastSuccessfulSyncUtc,
    string? LastSyncError,
    bool IsSyncing);

public sealed record GmailAuthorizationUrlResponse(
    string AuthorizationUrl);

public sealed record GmailSyncResultResponse(
    int MessagesScanned,
    int XmlAttachmentsFound,
    int Staged,
    int Duplicates,
    int RecognizedNotEnabled,
    int Rejected,
    int Failed,
    int AlreadyProcessed,
    DateTime CompletedAtUtc);
