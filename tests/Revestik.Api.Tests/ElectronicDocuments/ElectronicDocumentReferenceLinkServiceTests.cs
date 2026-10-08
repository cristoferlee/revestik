using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class ElectronicDocumentReferenceLinkServiceTests
{
    private const string Key = "50602102600310115777600500001010000051684120639723";
    private static readonly DateTimeOffset IssueDate = new(2026, 10, 2, 11, 12, 56, TimeSpan.FromHours(-6));

    [Fact]
    public async Task Suggest_ExactClaveAndParties_OffersMatchingDocument()
    {
        await using var db = CreateDb();
        var (note, original, reference) = Seed(db);
        var suggestions = await new ElectronicDocumentReferenceLinkService(db).SuggestAsync(note.Id);
        Assert.Equal(original.Id, Assert.Single(suggestions).SuggestedDocumentId);
        Assert.Equal(reference.Id, suggestions[0].ReferenceId);
        Assert.Equal("ExactMatch", suggestions[0].Result);
    }

    [Fact]
    public async Task Suggest_ConsecutiveOnly_RequiresManualReview()
    {
        await using var db = CreateDb();
        var (note, original, reference) = Seed(db);
        reference.ReferenceNumber = original.NumeroConsecutivo;
        await db.SaveChangesAsync();
        var suggestion = Assert.Single(await new ElectronicDocumentReferenceLinkService(db).SuggestAsync(note.Id));
        Assert.Null(suggestion.SuggestedDocumentId);
        Assert.Equal("RequiresManualReview", suggestion.Result);
    }

    [Fact]
    public async Task Link_ExactMatch_DoesNotApproveOrChangeAmounts()
    {
        await using var db = CreateDb();
        var (note, original, reference) = Seed(db);
        var total = note.TotalDocument;
        await new ElectronicDocumentReferenceLinkService(db).LinkAsync(reference.Id, original.Id);
        Assert.Equal(original.Id, reference.RelatedElectronicDocumentId);
        Assert.Equal(ElectronicDocumentAdjustmentStatus.PendingReview, note.AdjustmentStatus);
        Assert.Equal(total, note.TotalDocument);
        Assert.Equal(ElectronicDocumentProcessingStatus.Pending, note.ProcessingStatus);
    }

    [Fact]
    public async Task Link_DifferentIssuer_IsRejectedWithoutPersistence()
    {
        await using var db = CreateDb();
        var (_, original, reference) = Seed(db);
        original.IssuerIdentification = "9999999999";
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ElectronicDocumentReferenceLinkService(db).LinkAsync(reference.Id, original.Id));
        Assert.Null(reference.RelatedElectronicDocumentId);
    }

    [Fact]
    public async Task Link_ApprovedAdjustment_CannotChangeReference()
    {
        await using var db = CreateDb();
        var (note, original, reference) = Seed(db);
        note.AdjustmentStatus = ElectronicDocumentAdjustmentStatus.Approved;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ElectronicDocumentReferenceLinkService(db).LinkAsync(reference.Id, original.Id));
        Assert.Null(reference.RelatedElectronicDocumentId);
    }

    [Fact]
    public async Task Link_OriginalWithWrongDirection_IsRejected()
    {
        await using var db = CreateDb();
        var (_, original, reference) = Seed(db);
        original.Direction = ElectronicDocumentDirection.Issued;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ElectronicDocumentReferenceLinkService(db).LinkAsync(reference.Id, original.Id));
    }

    private static RevestikDbContext CreateDb() => new(
        new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"References-{Guid.NewGuid()}").Options);

    private static (ElectronicDocument Note, ElectronicDocument Original, ElectronicDocumentReference Reference)
        Seed(RevestikDbContext db)
    {
        var original = NewDocument(ElectronicDocumentType.Invoice, Key);
        original.NumeroConsecutivo = "00500001010000051684";
        var note = NewDocument(ElectronicDocumentType.CreditNote,
            "50602102600310115777600500001030000051684120639723");
        note.AdjustmentStatus = ElectronicDocumentAdjustmentStatus.PendingReview;
        var reference = new ElectronicDocumentReference
        {
            Sequence = 1,
            ReferencedDocumentTypeCode = "01",
            ReferenceNumber = Key,
            ReferencedIssueDate = IssueDate,
            ReferenceCode = "01",
            Reason = "Corrección"
        };
        note.References.Add(reference);
        db.ElectronicDocuments.AddRange(original, note);
        db.SaveChanges();
        return (note, original, reference);
    }

    private static ElectronicDocument NewDocument(ElectronicDocumentType type, string key) => new()
    {
        Clave = key,
        DocumentType = type,
        Direction = ElectronicDocumentDirection.Received,
        FechaEmision = IssueDate,
        IssuerIdentificationType = "02",
        IssuerIdentification = "3101157776",
        ReceiverIdentificationType = "02",
        ReceiverIdentification = "3102959852",
        TotalDocument = 100m,
        ProcessingStatus = ElectronicDocumentProcessingStatus.Pending
    };
}
