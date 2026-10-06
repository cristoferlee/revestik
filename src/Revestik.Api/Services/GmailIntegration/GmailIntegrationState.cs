namespace Revestik.Api.Services.GmailIntegration;

internal sealed class GmailIntegrationState
{
    public string? ConnectedMailbox { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ConnectedAtUtc { get; set; }
    public DateTime? LastSuccessfulSyncUtc { get; set; }
    public string? LastSyncError { get; set; }
    public List<GmailProcessedAttachment> ProcessedAttachments { get; set; } = [];
}

internal sealed class GmailProcessedAttachment
{
    public string Key { get; set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public int ValidationRevision { get; set; }
}
