namespace Revestik.Shared.Integrations.Gmail;

public sealed record BankVoucherGmailSyncResultResponse(
    int MessagesScanned,
    int Imported,
    int Duplicates,
    int Unrecognized,
    int Failed,
    DateTime CompletedAtUtc);
