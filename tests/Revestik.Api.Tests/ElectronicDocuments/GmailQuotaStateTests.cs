using System.Text.Json;
using Revestik.Api.Services.GmailIntegration;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class GmailQuotaStateTests
{
    [Fact]
    public void LegacyState_DeserializesWithNewSweepFields()
    {
        const string legacy = """{"ConnectedMailbox":"facturas@revestikcr.com","LastSuccessfulSyncUtc":null,"ProcessedAttachments":[]}""";
        var state = JsonSerializer.Deserialize<GmailIntegrationState>(legacy)!;
        Assert.Equal("facturas@revestikcr.com", state.ConnectedMailbox);
        Assert.False(state.HistoricalSweepCompleted);
        Assert.Null(state.SweepQuery);
        Assert.Empty(state.CompletedMessageIds);
    }

    [Fact]
    public void SweepState_RoundTripsWithoutDiscardingLedger()
    {
        var state = new GmailIntegrationState
        {
            ConnectedMailbox = "facturas@revestikcr.com",
            SweepQuery = "has:attachment filename:xml after:0 before:12345",
            SweepPageToken = "next-page",
            SweepStartedUtc = DateTime.UtcNow,
            CompletedMessageIds = ["message-1"],
            ProcessedAttachments = [new GmailProcessedAttachment { Key = "message-1:attachment-1", Outcome = "Staged:PendingReview" }]
        };
        var restored = JsonSerializer.Deserialize<GmailIntegrationState>(JsonSerializer.Serialize(state))!;
        Assert.Equal("next-page", restored.SweepPageToken);
        Assert.Equal("message-1", Assert.Single(restored.CompletedMessageIds));
        Assert.Single(restored.ProcessedAttachments);
    }
}
