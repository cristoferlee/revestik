using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class ElectronicDocumentReferenceReviewServiceTests
{
    [Fact]
    public async Task Review_NoteWithUnknownReference_DoesNotLinkOrApprove()
    {
        await using var db = CreateDb();
        var note = new ElectronicDocument
        {
            DocumentType = ElectronicDocumentType.CreditNote,
            AdjustmentStatus = ElectronicDocumentAdjustmentStatus.PendingReview,
            References = [new ElectronicDocumentReference
            {
                Sequence = 1, ReferenceNumber = "123", ReferencedDocumentTypeCode = "01",
                ReferencedIssueDate = DateTimeOffset.UtcNow
            }]
        };
        db.ElectronicDocuments.Add(note);
        await db.SaveChangesAsync();
        var service = new ElectronicDocumentReferenceReviewService(db,
            new ElectronicDocumentReferenceLinkService(db));

        var suggestions = await service.ReviewAsync(note.Id);

        Assert.Equal("RequiresManualReview", Assert.Single(suggestions).Result);
        Assert.Null(Assert.Single(note.References).RelatedElectronicDocumentId);
        Assert.Equal(ElectronicDocumentAdjustmentStatus.PendingReview, note.AdjustmentStatus);
    }

    [Fact]
    public async Task Review_ApprovedNote_IsRejected()
    {
        await using var db = CreateDb();
        var note = new ElectronicDocument
        {
            DocumentType = ElectronicDocumentType.DebitNote,
            AdjustmentStatus = ElectronicDocumentAdjustmentStatus.Approved
        };
        db.ElectronicDocuments.Add(note);
        await db.SaveChangesAsync();
        var service = new ElectronicDocumentReferenceReviewService(db,
            new ElectronicDocumentReferenceLinkService(db));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReviewAsync(note.Id));
    }

    private static RevestikDbContext CreateDb() => new(
        new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"FiscalLinkReview-{Guid.NewGuid()}").Options);
}
