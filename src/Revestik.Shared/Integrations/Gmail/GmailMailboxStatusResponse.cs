namespace Revestik.Shared.Integrations.Gmail;

public sealed record GmailMailboxStatusResponse(
    bool IsConfigured,
    bool IsConnected,
    string ExpectedMailbox,
    string? ConnectedMailbox,
    DateTime? ConnectedAtUtc,
    DateTime? LastSuccessfulSyncUtc,
    string? LastSyncError,
    bool IsSyncing);

public sealed record GmailMailboxAuthorizationUrlResponse(
    string AuthorizationUrl);
