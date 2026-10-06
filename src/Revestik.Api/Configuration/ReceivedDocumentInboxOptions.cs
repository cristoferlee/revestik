namespace Revestik.Api.Configuration;

public sealed class ReceivedDocumentInboxOptions
{
    public const string SectionName = "ReceivedDocumentInbox";

    public string StoragePath { get; set; } = "App_Data/received-documents-inbox";
    public decimal HighAmountReviewThresholdCrc { get; set; } = 100_000_000m;
    public decimal MonetaryTolerance { get; set; } = 1m;
}
