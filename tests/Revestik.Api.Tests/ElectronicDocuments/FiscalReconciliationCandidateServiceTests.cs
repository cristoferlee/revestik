using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Services.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class FiscalReconciliationCandidateServiceTests
{
    private static RevestikDbContext CreateDb() => new(
        new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"FiscalCandidates-{Guid.NewGuid()}").Options);

    [Theory]
    [InlineData("invoices")]
    [InlineData("notes")]
    [InlineData("other")]
    public async Task EmptyWorklist_ReturnsEmptyPage(string kind)
    {
        await using var db = CreateDb();
        var result = await new FiscalReconciliationCandidateService(db)
            .ListAsync(kind, null, 1, 20, CancellationToken.None);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 101)]
    public async Task InvalidPaging_IsRejected(int page, int size)
    {
        await using var db = CreateDb();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new FiscalReconciliationCandidateService(db)
                .ListAsync("invoices", null, page, size, CancellationToken.None));
    }

    [Fact]
    public async Task NotePreview_IncludesApprovedAndAppliedNotes_WithoutChangingDocuments()
    {
        await using var db = CreateDb();
        var issueDate = new DateTimeOffset(2026, 10, 2, 11, 12, 0, TimeSpan.FromHours(-6));
        db.ElectronicDocuments.AddRange(
            new Revestik.Api.Models.ElectronicDocument
            {
                Id = 1, Clave = "invoice", CurrencyCode = "CRC", TotalDocument = 150000m,
                FechaEmision = issueDate,
                Direction = Revestik.Shared.ElectronicDocuments.ElectronicDocumentDirection.Received,
                IssuerIdentificationType = "02", IssuerIdentification = "3101157776",
                ReceiverIdentificationType = "02", ReceiverIdentification = "3102959852",
                DocumentType = Revestik.Shared.ElectronicDocuments.ElectronicDocumentType.Invoice
            },
            new Revestik.Api.Models.ElectronicDocument
            {
                Id = 2, Clave = "approved", CurrencyCode = "CRC", TotalDocument = 30000m,
                DocumentType = Revestik.Shared.ElectronicDocuments.ElectronicDocumentType.CreditNote,
                AdjustmentStatus = Revestik.Shared.ElectronicDocuments.ElectronicDocumentAdjustmentStatus.Approved,
                References = [new Revestik.Api.Models.ElectronicDocumentReference { Id = 10, RelatedElectronicDocumentId = 1 }]
            },
            new Revestik.Api.Models.ElectronicDocument
            {
                Id = 3, Clave = "applied", CurrencyCode = "CRC", TotalDocument = 10000m,
                DocumentType = Revestik.Shared.ElectronicDocuments.ElectronicDocumentType.DebitNote,
                AdjustmentStatus = Revestik.Shared.ElectronicDocuments.ElectronicDocumentAdjustmentStatus.Applied,
                References = [new Revestik.Api.Models.ElectronicDocumentReference { Id = 11, RelatedElectronicDocumentId = 1 }]
            },
            new Revestik.Api.Models.ElectronicDocument
            {
                Id = 4, Clave = "pending", CurrencyCode = "CRC", TotalDocument = 20000m,
                Direction = Revestik.Shared.ElectronicDocuments.ElectronicDocumentDirection.Received,
                IssuerIdentificationType = "02", IssuerIdentification = "3101157776",
                ReceiverIdentificationType = "02", ReceiverIdentification = "3102959852",
                DocumentType = Revestik.Shared.ElectronicDocuments.ElectronicDocumentType.CreditNote,
                AdjustmentStatus = Revestik.Shared.ElectronicDocuments.ElectronicDocumentAdjustmentStatus.PendingReview,
                References = [new Revestik.Api.Models.ElectronicDocumentReference
                {
                    Id = 12, RelatedElectronicDocumentId = 1,
                    ReferenceNumber = "invoice", ReferencedDocumentTypeCode = "01",
                    ReferencedIssueDate = issueDate
                }]
            });
        await db.SaveChangesAsync();
        var result = await new FiscalReconciliationCandidateService(db).NoteAsync(4, CancellationToken.None);
        var preview = Assert.IsType<Revestik.Shared.ElectronicDocuments.FiscalNoteAdjustmentPreview>(result.Preview);
        Assert.Equal(130000m, preview.CurrentAdjustedTotal);
        Assert.Equal(110000m, preview.ProjectedTotal);
        Assert.Equal(30000m, preview.ApprovedCreditNotesTotal);
        Assert.Equal(10000m, preview.ApprovedDebitNotesTotal);
        Assert.Equal(150000m, (await db.ElectronicDocuments.FindAsync(1))!.TotalDocument);
        Assert.Equal(Revestik.Shared.ElectronicDocuments.ElectronicDocumentAdjustmentStatus.PendingReview,
            (await db.ElectronicDocuments.FindAsync(4))!.AdjustmentStatus);
    }
}
