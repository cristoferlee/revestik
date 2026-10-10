using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class FiscalApprovalTotalsTests
{
    private static RevestikDbContext CreateDb() => new(
        new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"FiscalApproval-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task ApprovedCreditAndDebit_AdjustFiscalTotal_WithoutChangingOriginal()
    {
        await using var db = CreateDb();
        var original = new ElectronicDocument
        {
            Id = 1, Clave = "original", DocumentType = ElectronicDocumentType.Invoice,
            CurrencyCode = "CRC", TotalDocument = 150000m
        };
        var credit = new ElectronicDocument
        {
            Id = 2, Clave = "credit", DocumentType = ElectronicDocumentType.CreditNote,
            CurrencyCode = "CRC", TotalDocument = 30000m,
            AdjustmentStatus = ElectronicDocumentAdjustmentStatus.Approved,
            References = [new ElectronicDocumentReference { Id = 4, RelatedElectronicDocumentId = 1 }]
        };
        var debit = new ElectronicDocument
        {
            Id = 3, Clave = "debit", DocumentType = ElectronicDocumentType.DebitNote,
            CurrencyCode = "CRC", TotalDocument = 10000m,
            AdjustmentStatus = ElectronicDocumentAdjustmentStatus.Approved,
            References = [new ElectronicDocumentReference { Id = 5, RelatedElectronicDocumentId = 1 }]
        };
        db.ElectronicDocuments.AddRange(original, credit, debit);
        await db.SaveChangesAsync();

        var adjusted = Assert.Single(await new FiscalApprovalService(db)
            .GetAdjustedTotalsAsync([1], CancellationToken.None));

        Assert.Equal(150000m, adjusted.OriginalTotal);
        Assert.Equal(30000m, adjusted.CreditNotesTotal);
        Assert.Equal(10000m, adjusted.DebitNotesTotal);
        Assert.Equal(130000m, adjusted.AdjustedTotal);
        Assert.Equal(2, adjusted.Adjustments.Count);
        Assert.Equal(150000m, (await db.ElectronicDocuments.FindAsync(1))!.TotalDocument);
    }

    [Fact]
    public async Task PendingAndMultiReferenceNotes_DoNotAffectAdjustedAmount()
    {
        await using var db = CreateDb();
        db.ElectronicDocuments.AddRange(
            new ElectronicDocument { Id = 1, Clave = "a", DocumentType = ElectronicDocumentType.Invoice,
                CurrencyCode = "CRC", TotalDocument = 100m },
            new ElectronicDocument { Id = 2, Clave = "b", DocumentType = ElectronicDocumentType.CreditNote,
                CurrencyCode = "CRC", TotalDocument = 30m,
                AdjustmentStatus = ElectronicDocumentAdjustmentStatus.PendingReview,
                References = [new ElectronicDocumentReference { Id = 6, RelatedElectronicDocumentId = 1 }] },
            new ElectronicDocument { Id = 3, Clave = "c", DocumentType = ElectronicDocumentType.CreditNote,
                CurrencyCode = "CRC", TotalDocument = 20m,
                AdjustmentStatus = ElectronicDocumentAdjustmentStatus.Approved,
                References = [new ElectronicDocumentReference { Id = 7, RelatedElectronicDocumentId = 1 },
                    new ElectronicDocumentReference { Id = 8, ReferenceNumber = "other" }] });
        await db.SaveChangesAsync();

        var adjusted = Assert.Single(await new FiscalApprovalService(db)
            .GetAdjustedTotalsAsync([1], CancellationToken.None));
        Assert.Equal(100m, adjusted.AdjustedTotal);
        Assert.Empty(adjusted.Adjustments);
    }
}
