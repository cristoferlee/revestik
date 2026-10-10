using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

/// <summary>
/// SQL Server-only diagnostic tests. Uses the existing isolated Testcontainers fixture.
/// No production database is accessed or modified.
/// </summary>
public sealed class FiscalApprovalIntegritySqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private static readonly DateTimeOffset IssuedAt =
        new(2026, 10, 2, 10, 0, 0, TimeSpan.FromHours(-6));

    [Fact]
    public async Task ApproveOnce_SecondAttemptRejected_AndTotalsNotDuplicated()
    {
        var (noteId, originalId, referenceId) = await SeedAsync();
        await using (var firstDb = fixture.CreateDbContext())
            await new FiscalApprovalService(firstDb).ApproveAsync(
                noteId, new FiscalApprovalRequest(referenceId, originalId), CancellationToken.None);

        await using (var retryDb = fixture.CreateDbContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new FiscalApprovalService(retryDb).ApproveAsync(
                    noteId, new FiscalApprovalRequest(referenceId, originalId), CancellationToken.None));

        await VerifyAsync(noteId, originalId, referenceId, true);
    }

    [Fact]
    public async Task FailureAfterLink_RollsBackLinkAndStatus()
    {
        // The link is valid, but the note's state is not PendingReview.
        // The conditional approval must fail after attempting the link;
        // the transaction must undo that link as well.
        var (noteId, originalId, referenceId) = await SeedAsync(
            ElectronicDocumentAdjustmentStatus.Applied, useNonApprovedFailure: true);

        await using (var db = fixture.CreateDbContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new FiscalApprovalService(db).ApproveAsync(
                    noteId, new FiscalApprovalRequest(referenceId, originalId), CancellationToken.None));

        await using var verify = fixture.CreateDbContext();
        var note = await verify.ElectronicDocuments.AsNoTracking().SingleAsync(x => x.Id == noteId);
        var reference = await verify.Set<ElectronicDocumentReference>().AsNoTracking().SingleAsync(x => x.Id == referenceId);
        Assert.Null(note.AdjustmentStatus);
        Assert.Null(reference.RelatedElectronicDocumentId);
        Assert.Equal(100m, (await verify.ElectronicDocuments.AsNoTracking().SingleAsync(x => x.Id == originalId)).TotalDocument);
    }

    [Fact]
    public async Task ConcurrentApprovals_CannotBothSucceed_OrDuplicateAdjustment()
    {
        var (noteId, originalId, referenceId) = await SeedAsync();
        using var gate = new ManualResetEventSlim(false);
        async Task<Exception?> AttemptAsync()
        {
            await using var db = fixture.CreateDbContext();
            await Task.Run(() => gate.Wait(TimeSpan.FromSeconds(10)));
            try
            {
                await new FiscalApprovalService(db).ApproveAsync(
                    noteId, new FiscalApprovalRequest(referenceId, originalId), CancellationToken.None);
                return null;
            }
            catch (Exception error)
            {
                return error;
            }
        }

        var one = Task.Run(AttemptAsync);
        var two = Task.Run(AttemptAsync);
        gate.Set();
        var results = await Task.WhenAll(one, two);
        Assert.Single(results, x => x is null);
        Assert.Single(results, x => x is not null);
        await VerifyAsync(noteId, originalId, referenceId, true);
    }

    private async Task VerifyAsync(int noteId, int originalId, int referenceId, bool approved)
    {
        await using var db = fixture.CreateDbContext();
        var note = await db.ElectronicDocuments.AsNoTracking().SingleAsync(x => x.Id == noteId);
        var original = await db.ElectronicDocuments.AsNoTracking().SingleAsync(x => x.Id == originalId);
        var link = await db.Set<ElectronicDocumentReference>().AsNoTracking().SingleAsync(x => x.Id == referenceId);
        Assert.Equal(approved ? ElectronicDocumentAdjustmentStatus.Approved : ElectronicDocumentAdjustmentStatus.PendingReview,
            note.AdjustmentStatus);
        Assert.Equal(approved ? originalId : (int?)null, link.RelatedElectronicDocumentId);
        Assert.Equal(100m, original.TotalDocument);
        var total = Assert.Single(await new FiscalApprovalService(db).GetAdjustedTotalsAsync(
            [originalId], CancellationToken.None));
        Assert.Equal(approved ? 70m : 100m, total.AdjustedTotal);
        Assert.Equal(approved ? 1 : 0, total.Adjustments.Count);
    }

    private async Task<(int NoteId, int OriginalId, int ReferenceId)> SeedAsync(
        ElectronicDocumentAdjustmentStatus status = ElectronicDocumentAdjustmentStatus.PendingReview,
        bool useNonApprovedFailure = false)
    {
        await using var db = fixture.CreateDbContext();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("N"),
            UserName = $"fiscal-sql-{Guid.NewGuid():N}@example.test",
            NormalizedUserName = $"FISCAL-SQL-{Guid.NewGuid():N}@EXAMPLE.TEST"
        };
        db.Users.Add(user);
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var original = MakeDocument("O" + suffix, ElectronicDocumentType.Invoice, user.Id, 100m);
        var note = MakeDocument("N" + suffix, ElectronicDocumentType.CreditNote, user.Id, 30m);
        note.AdjustmentStatus = useNonApprovedFailure ? null : status;
        var reference = new ElectronicDocumentReference
        {
            Sequence = 1,
            ReferencedDocumentTypeCode = "01",
            ReferenceNumber = original.NumeroConsecutivo,
            ReferencedIssueDate = IssuedAt,
            ReferenceCode = "01",
            Reason = "Prueba de integridad SQL"
        };
        note.References.Add(reference);
        db.ElectronicDocuments.AddRange(original, note);
        await db.SaveChangesAsync();
        return (note.Id, original.Id, reference.Id);
    }

    private static ElectronicDocument MakeDocument(string key, ElectronicDocumentType type,
        string userId, decimal amount) => new()
        {
            Clave = key,
            NumeroConsecutivo = "00500001010000051684",
            DocumentType = type,
            Direction = ElectronicDocumentDirection.Received,
            FechaEmision = IssuedAt,
            IssuerIdentificationType = "02",
            IssuerIdentification = "3101157776",
            ReceiverIdentificationType = "02",
            ReceiverIdentification = "3102959852",
            CurrencyCode = "CRC",
            ExchangeRate = 1m,
            TotalDocument = amount,
            ProcessingStatus = ElectronicDocumentProcessingStatus.Pending,
            ImportedByUserId = userId,
            ImportedAtUtc = DateTime.UtcNow
        };
}
