using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

/// <summary>Real SQL Server regressions for an auditable date exception.
/// Uses Revestik's shared Testcontainers fixture and its class-isolated database.</summary>
public sealed class FiscalReferenceResolutionSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private static int nextFiscalSuffix;

    private static readonly DateTimeOffset IssuedAt =
        new(2026, 10, 2, 13, 14, 44, TimeSpan.FromHours(-6));
    private const string Reason = "Validación manual: clave e identidades coinciden; fecha errónea del proveedor.";

    [Fact]
    public async Task DateException_PersistsAudit_WithoutLinkOrApproval()
    {
        var (referenceId, originalId, noteId, userId) = await SeedAsync();
        await using (var db = fixture.CreateDbContext())
        {
            var decision = await new FiscalReferenceResolutionService(db).AuthorizeDateExceptionAsync(
                referenceId, new FiscalDateExceptionRequest(originalId, Reason), userId, default);
            Assert.Equal(referenceId, decision.ReferenceId);
            Assert.Equal(originalId, decision.OriginalDocumentId);
            Assert.Equal(userId, decision.ResolvedByUserId);
            Assert.Equal("DateExceptionAuthorized", decision.ResolutionType);
        }
        await using var verify = fixture.CreateDbContext();
        var saved = await verify.ElectronicDocumentReferenceResolutions.AsNoTracking()
            .SingleAsync(x => x.ElectronicDocumentReferenceId == referenceId);
        Assert.Equal(Reason, saved.Justification);
        Assert.NotEqual(default, saved.ResolvedAtUtc);
        var reference = await verify.ElectronicDocumentReferences.AsNoTracking().SingleAsync(x => x.Id == referenceId);
        Assert.Null(reference.RelatedElectronicDocumentId);
        var note = await verify.ElectronicDocuments.AsNoTracking().SingleAsync(x => x.Id == noteId);
        Assert.Equal(ElectronicDocumentAdjustmentStatus.PendingReview, note.AdjustmentStatus);
        Assert.Equal(142054.56m, note.TotalDocument);
        Assert.Equal(new byte[] { 1, 2, 3 }, note.OriginalXml);
    }

    [Fact]
    public async Task TwoIndependentContexts_CannotDuplicateDateAuthorization()
    {
        var (referenceId, originalId, _, userId) = await SeedAsync();
        await using var first = fixture.CreateDbContext();
        await using var second = fixture.CreateDbContext();
        // Simulate two requests that both inspected the same pending reference.
        await first.ElectronicDocumentReferences.AsNoTracking().SingleAsync(x => x.Id == referenceId);
        await second.ElectronicDocumentReferences.AsNoTracking().SingleAsync(x => x.Id == referenceId);

        await new FiscalReferenceResolutionService(first).AuthorizeDateExceptionAsync(
            referenceId, new FiscalDateExceptionRequest(originalId, Reason), userId, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new FiscalReferenceResolutionService(second).AuthorizeDateExceptionAsync(
                referenceId, new FiscalDateExceptionRequest(originalId, Reason), userId, default));

        await using var verify = fixture.CreateDbContext();
        Assert.Equal(1, await verify.ElectronicDocumentReferenceResolutions.AsNoTracking()
            .CountAsync(x => x.ElectronicDocumentReferenceId == referenceId));
    }

    [Fact]
    public async Task TwoSimultaneousRequests_AtMostOneAuthorizationIsPersisted()
    {
        var (referenceId, originalId, _, userId) = await SeedAsync();

        // Each request uses its own DbContext/connection, like independent HTTP requests.
        // A gate starts both authorization calls together, rather than sequentially.
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<Exception?> AttemptAsync()
        {
            await using var context = fixture.CreateDbContext();
            await start.Task;
            try
            {
                await new FiscalReferenceResolutionService(context).AuthorizeDateExceptionAsync(
                    referenceId, new FiscalDateExceptionRequest(originalId, Reason), userId, default);
                return null;
            }
            catch (Exception error)
            {
                return error;
            }
        }

        var first = Task.Run(AttemptAsync);
        var second = Task.Run(AttemptAsync);
        start.SetResult();
        var outcomes = await Task.WhenAll(first, second);

        // SQL Server may reject the loser as a duplicate or a serialization deadlock.
        // Both outcomes cannot succeed, and the winning decision must be durable.
        Assert.Single(outcomes, error => error is null);
        Assert.Single(outcomes, error => error is not null);

        await using var verify = fixture.CreateDbContext();
        var decisions = await verify.ElectronicDocumentReferenceResolutions.AsNoTracking()
            .Where(x => x.ElectronicDocumentReferenceId == referenceId)
            .ToListAsync();
        Assert.Single(decisions);
        Assert.Equal(originalId, decisions[0].OriginalDocumentId);
        var reference = await verify.ElectronicDocumentReferences.AsNoTracking()
            .SingleAsync(x => x.Id == referenceId);
        Assert.Null(reference.RelatedElectronicDocumentId);
    }

    [Fact]
    public async Task ChangedNoteStatus_IsRejected_WithoutAuditRecord()
    {
        var (referenceId, originalId, noteId, userId) = await SeedAsync();
        await using (var db = fixture.CreateDbContext())
        {
            var note = await db.ElectronicDocuments.SingleAsync(x => x.Id == noteId);
            note.AdjustmentStatus = ElectronicDocumentAdjustmentStatus.Approved;
            await db.SaveChangesAsync();
        }
        await using var attempt = fixture.CreateDbContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new FiscalReferenceResolutionService(attempt).AuthorizeDateExceptionAsync(
                referenceId, new FiscalDateExceptionRequest(originalId, Reason), userId, default));
        Assert.False(await attempt.ElectronicDocumentReferenceResolutions.AnyAsync(
            x => x.ElectronicDocumentReferenceId == referenceId));
    }

    private async Task<(int ReferenceId, int OriginalId, int NoteId, string UserId)> SeedAsync()
    {
        await using var db = fixture.CreateDbContext();
        var userId = Guid.NewGuid().ToString("N");
        db.Users.Add(new ApplicationUser
        {
            Id = userId, UserName = $"resolution-{userId}@example.test",
            NormalizedUserName = $"RESOLUTION-{userId}@EXAMPLE.TEST", IsActive = true
        });
        // Tests in this class share one SQL database; fiscal keys must be unique per seed.
        // Modify only the 8-digit security-code suffix, preserving 50-digit fiscal keys.
        var suffix = Interlocked.Increment(ref nextFiscalSuffix).ToString("D8");
        var key = "506021026003101157776005000010100000516841" + suffix;
        var noteKey = "506021026003101157776005000010300000516841" + suffix;
        var original = MakeDocument(key, ElectronicDocumentType.Invoice, userId);
        var note = MakeDocument(noteKey, ElectronicDocumentType.CreditNote, userId);
        note.AdjustmentStatus = ElectronicDocumentAdjustmentStatus.PendingReview;
        note.TotalDocument = 142054.56m;
        var reference = new ElectronicDocumentReference
        {
            Sequence = 1, ReferencedDocumentTypeCode = "01", ReferenceNumber = key,
            ReferencedIssueDate = IssuedAt.AddDays(3), ReferenceCode = "06", Reason = "Devolución"
        };
        note.References.Add(reference);
        db.ElectronicDocuments.AddRange(original, note);
        await db.SaveChangesAsync();
        return (reference.Id, original.Id, note.Id, userId);
    }

    private static ElectronicDocument MakeDocument(string key, ElectronicDocumentType kind, string userId) => new()
    {
        Clave = key, NumeroConsecutivo = "00500001010000051684", DocumentType = kind,
        Direction = ElectronicDocumentDirection.Received, FechaEmision = IssuedAt,
        IssuerIdentificationType = "02", IssuerIdentification = "3101157776",
        ReceiverIdentificationType = "02", ReceiverIdentification = "3102959852",
        CurrencyCode = "CRC", ExchangeRate = 1m, TotalDocument = 152631.36m,
        OriginalXml = [1, 2, 3], ImportedByUserId = userId, ImportedAtUtc = DateTime.UtcNow
    };
}
