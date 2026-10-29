namespace Revestik.Client.Services.ElectronicDocuments;

public sealed record GmailHistoricalSyncProgress(
    bool Queued, bool Running, int Pages, int MessagesScanned,
    int Staged, int Duplicates, int Failed, string? Error,
    DateTime? StartedUtc, DateTime? FinishedUtc);
