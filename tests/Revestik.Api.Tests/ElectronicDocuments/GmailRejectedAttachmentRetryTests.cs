using Revestik.Api.Services.GmailIntegration;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class GmailRejectedAttachmentRetryTests
{
    [Fact]
    public void RemoveRetryableRejectedAttachments_RemovesOnlyOldRejectedEntries()
    {
        var state = new GmailIntegrationState
        {
            ProcessedAttachments =
            [
                new GmailProcessedAttachment
                {
                    Key = "old-rejected",
                    Outcome = "Rejected:XSD warning treated as error",
                    ValidationRevision = 0,
                    ProcessedAtUtc = DateTime.UtcNow
                },
                new GmailProcessedAttachment
                {
                    Key = "current-rejected",
                    Outcome = "Rejected:Really invalid",
                    ValidationRevision = 2,
                    ProcessedAtUtc = DateTime.UtcNow
                },
                new GmailProcessedAttachment
                {
                    Key = "staged",
                    Outcome = "Staged:Pending",
                    ValidationRevision = 0,
                    ProcessedAtUtc = DateTime.UtcNow
                }
            ]
        };

        GmailIntegrationService.RemoveRetryableRejectedAttachments(state);

        Assert.DoesNotContain(state.ProcessedAttachments, x => x.Key == "old-rejected");
        Assert.Contains(state.ProcessedAttachments, x => x.Key == "current-rejected");
        Assert.Contains(state.ProcessedAttachments, x => x.Key == "staged");
    }
}
