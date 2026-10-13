using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

/// <summary>
/// B3/B4: exercises the existing transactional approval service against real SQL Server.
/// B2 already routes reference links through the audited date-exception validator.
/// </summary>
public sealed class FiscalExceptionalApprovalSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private static readonly DateTimeOffset IssuedAt =
        new(2026, 10, 2, 13, 14, 44, TimeSpan.FromHours(-6));

    [Fact]
    public async Task AuthorizedDateException_ApprovesAndLinksInOneTransaction()
    {
        var s = await SeedAsync(authorized: true);
        await using (var db = fixture.CreateDbContext())
            await new FiscalApprovalService(db).ApproveAsync(s.NoteId,
                new FiscalApprovalRequest(s.ReferenceId, s.OriginalId), CancellationToken.None);

        await using var verify = fixture.CreateDbContext();
        var note = await verify.ElectronicDocuments.AsNoTracking().SingleAsync(x => x.Id == s.NoteId);
        var reference = await verify.ElectronicDocumentReferences.AsNoTracking()
            .SingleAsync(x => x.Id == s.ReferenceId);
        Assert.Equal(ElectronicDocumentAdjustmentStatus.Approved, note.AdjustmentStatus);
        Assert.Equal(s.OriginalId, reference.RelatedElectronicDocumentId);
        Assert.Equal(142054.56m, note.TotalDocument);
        Assert.Equal(new byte[] { 1, 2, 3 }, note.OriginalXml);
        Assert.Single(await verify.ElectronicDocumentReferenceResolutions.AsNoTracking()
            .Where(x => x.ElectronicDocumentReferenceId == s.ReferenceId).ToListAsync());
    }

    [Fact]
    public async Task WithoutAuthorization_ApprovalFailsAndDoesNotLink()
    {
        var s = await SeedAsync(authorized: false);
        await using (var db = fixture.CreateDbContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new FiscalApprovalService(db).ApproveAsync(s.NoteId,
                    new FiscalApprovalRequest(s.ReferenceId, s.OriginalId), CancellationToken.None));
        await AssertPendingAndUnlinkedAsync(s);
    }

    [Fact]
    public async Task CurrencyChangedAfterAuthorization_ApprovalFailsAndRollsBack()
    {
        var s = await SeedAsync(authorized: true);
        await using (var db = fixture.CreateDbContext())
        {
            var original = await db.ElectronicDocuments.SingleAsync(x => x.Id == s.OriginalId);
            original.CurrencyCode = "USD";
            await db.SaveChangesAsync();
        }
        await using (var db = fixture.CreateDbContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new FiscalApprovalService(db).ApproveAsync(s.NoteId,
                    new FiscalApprovalRequest(s.ReferenceId, s.OriginalId), CancellationToken.None));
        await AssertPendingAndUnlinkedAsync(s);
    }

    [Fact]
    public async Task SecondApprovalCannotApplyNoteTwice()
    {
        var s = await SeedAsync(authorized: true);
        await using (var db = fixture.CreateDbContext())
            await new FiscalApprovalService(db).ApproveAsync(s.NoteId,
                new FiscalApprovalRequest(s.ReferenceId, s.OriginalId), CancellationToken.None);
        await using (var second = fixture.CreateDbContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new FiscalApprovalService(second).ApproveAsync(s.NoteId,
                    new FiscalApprovalRequest(s.ReferenceId, s.OriginalId), CancellationToken.None));
        await using var verify = fixture.CreateDbContext();
        var adjusted = await new FiscalApprovalService(verify).GetAdjustedTotalsAsync(
            [s.OriginalId], CancellationToken.None);
        var total = Assert.Single(adjusted);
        Assert.Equal(142054.56m, total.CreditNotesTotal);
        Assert.Equal(152631.36m - 142054.56m, total.AdjustedTotal);
        Assert.Single(total.Adjustments);
    }

    [Fact]
    public async Task MultipleReferencesCannotBeApprovedWithOneAuthorization()
    {
        var s = await SeedAsync(authorized: true);
        await using (var db = fixture.CreateDbContext())
        {
            db.ElectronicDocumentReferences.Add(new ElectronicDocumentReference
            {
                ElectronicDocumentId = s.NoteId, Sequence = 2,
                ReferencedDocumentTypeCode = "01", ReferenceNumber = s.OriginalKey,
                ReferencedIssueDate = IssuedAt, ReferenceCode = "06", Reason = "Otra referencia"
            });
            await db.SaveChangesAsync();
        }
        await using (var db = fixture.CreateDbContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new FiscalApprovalService(db).ApproveAsync(s.NoteId,
                    new FiscalApprovalRequest(s.ReferenceId, s.OriginalId), CancellationToken.None));
        await AssertPendingAndUnlinkedAsync(s);
    }

    [Fact]
    public async Task PrelinkedAuthorizedReference_CanBeApprovedWithoutDuplicatingLink()
    {
        var s = await SeedAsync(authorized: true);
        await using (var db = fixture.CreateDbContext())
            await new ElectronicDocumentReferenceLinkService(db).LinkAsync(s.ReferenceId, s.OriginalId);
        await using (var db = fixture.CreateDbContext())
            await new FiscalApprovalService(db).ApproveAsync(s.NoteId,
                new FiscalApprovalRequest(s.ReferenceId, s.OriginalId), CancellationToken.None);
        await using var verify = fixture.CreateDbContext();
        Assert.Equal(s.OriginalId, (await verify.ElectronicDocumentReferences.AsNoTracking()
            .SingleAsync(x => x.Id == s.ReferenceId)).RelatedElectronicDocumentId);
        Assert.Equal(ElectronicDocumentAdjustmentStatus.Approved,
            (await verify.ElectronicDocuments.AsNoTracking().SingleAsync(x => x.Id == s.NoteId)).AdjustmentStatus);
    }

    [Fact]
    public async Task TwoSimultaneousApprovals_OnlyOneSucceedsAndAdjustmentIsCountedOnce()
    {
        var s = await SeedAsync(authorized: true);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<Exception?> AttemptAsync()
        {
            await using var db = fixture.CreateDbContext();
            await start.Task;
            try
            {
                await new FiscalApprovalService(db).ApproveAsync(
                    s.NoteId,
                    new FiscalApprovalRequest(s.ReferenceId, s.OriginalId),
                    CancellationToken.None);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        var first = Task.Run(AttemptAsync);
        var second = Task.Run(AttemptAsync);
        start.SetResult();
        var outcomes = await Task.WhenAll(first, second);

        Assert.Single(outcomes, error => error is null);
        Assert.Single(outcomes, error => error is not null);

        await using var verify = fixture.CreateDbContext();
        var note = await verify.ElectronicDocuments.AsNoTracking()
            .SingleAsync(x => x.Id == s.NoteId);
        var reference = await verify.ElectronicDocumentReferences.AsNoTracking()
            .SingleAsync(x => x.Id == s.ReferenceId);
        Assert.Equal(ElectronicDocumentAdjustmentStatus.Approved, note.AdjustmentStatus);
        Assert.Equal(s.OriginalId, reference.RelatedElectronicDocumentId);

        var totals = await new FiscalApprovalService(verify).GetAdjustedTotalsAsync(
            [s.OriginalId], CancellationToken.None);
        var total = Assert.Single(totals);
        Assert.Single(total.Adjustments);
        Assert.Equal(142054.56m, total.CreditNotesTotal);
        Assert.Equal(152631.36m - 142054.56m, total.AdjustedTotal);
    }

    [Fact]
    public async Task ExceptionalApproval_DoesNotWriteToOperationalOrInventoryTables()
    {
        var s = await SeedAsync(authorized: true);
        await using var db = fixture.CreateDbContext();

        var before = new
        {
            Purchases = await db.Purchases.CountAsync(),
            PurchaseLines = await db.PurchaseLines.CountAsync(),
            PurchasePayments = await db.PurchasePayments.CountAsync(),
            Expenses = await db.Expenses.CountAsync(),
            BankVouchers = await db.BankVouchers.CountAsync(),
            InventoryMovements = await db.InventoryMovements.CountAsync(),
            InventoryCostLayers = await db.InventoryCostLayers.CountAsync(),
            InventoryCostConsumptions = await db.InventoryCostConsumptions.CountAsync(),
            Sales = await db.Sales.CountAsync(),
            SalePayments = await db.SalePayments.CountAsync()
        };

        await new FiscalApprovalService(db).ApproveAsync(
            s.NoteId,
            new FiscalApprovalRequest(s.ReferenceId, s.OriginalId),
            CancellationToken.None);

        await using var verify = fixture.CreateDbContext();
        var after = new
        {
            Purchases = await verify.Purchases.CountAsync(),
            PurchaseLines = await verify.PurchaseLines.CountAsync(),
            PurchasePayments = await verify.PurchasePayments.CountAsync(),
            Expenses = await verify.Expenses.CountAsync(),
            BankVouchers = await verify.BankVouchers.CountAsync(),
            InventoryMovements = await verify.InventoryMovements.CountAsync(),
            InventoryCostLayers = await verify.InventoryCostLayers.CountAsync(),
            InventoryCostConsumptions = await verify.InventoryCostConsumptions.CountAsync(),
            Sales = await verify.Sales.CountAsync(),
            SalePayments = await verify.SalePayments.CountAsync()
        };
        Assert.Equal(before, after);

        var note = await verify.ElectronicDocuments.AsNoTracking()
            .SingleAsync(x => x.Id == s.NoteId);
        var original = await verify.ElectronicDocuments.AsNoTracking()
            .SingleAsync(x => x.Id == s.OriginalId);
        Assert.Equal(new byte[] { 1, 2, 3 }, note.OriginalXml);
        Assert.Equal(new byte[] { 1, 2, 3 }, original.OriginalXml);
        Assert.Equal(142054.56m, note.TotalDocument);
        Assert.Equal(152631.36m, original.TotalDocument);
    }

    private async Task AssertPendingAndUnlinkedAsync(SeedResult s)
    {
        await using var db = fixture.CreateDbContext();
        Assert.Equal(ElectronicDocumentAdjustmentStatus.PendingReview,
            (await db.ElectronicDocuments.AsNoTracking().SingleAsync(x => x.Id == s.NoteId)).AdjustmentStatus);
        Assert.Null((await db.ElectronicDocumentReferences.AsNoTracking()
            .SingleAsync(x => x.Id == s.ReferenceId)).RelatedElectronicDocumentId);
    }

    private async Task<SeedResult> SeedAsync(bool authorized)
    {
        await using var db = fixture.CreateDbContext();
        var unique = Guid.NewGuid().ToString("N");
        var userId = unique;
        var suffix = Convert.ToUInt32(unique[..8], 16).ToString("D10");
        var originalKey = "5060210260031011577760050000101000005168" + suffix;
        var noteKey = "5060210260031011577760050000103000005168" + suffix;
        db.Users.Add(new ApplicationUser
        {
            Id = userId, UserName = $"b3-{unique}@example.test",
            NormalizedUserName = $"B3-{unique.ToUpperInvariant()}@EXAMPLE.TEST",
            IsActive = true
        });
        var original = MakeDocument(originalKey, ElectronicDocumentType.Invoice, userId);
        var note = MakeDocument(noteKey, ElectronicDocumentType.CreditNote, userId);
        note.AdjustmentStatus = ElectronicDocumentAdjustmentStatus.PendingReview;
        note.TotalDocument = 142054.56m;
        var reference = new ElectronicDocumentReference
        {
            Sequence = 1, ReferencedDocumentTypeCode = "01", ReferenceNumber = originalKey,
            ReferencedIssueDate = IssuedAt.AddDays(3), ReferenceCode = "06", Reason = "Devolución"
        };
        note.References.Add(reference);
        db.ElectronicDocuments.AddRange(original, note);
        await db.SaveChangesAsync();
        if (authorized)
        {
            db.ElectronicDocumentReferenceResolutions.Add(new ElectronicDocumentReferenceResolution
            {
                ElectronicDocumentReferenceId = reference.Id,
                OriginalDocumentId = original.Id,
                ResolutionType = "DateExceptionAuthorized",
                Justification = "Verificada manualmente la clave y la identidad fiscal; difiere solamente la fecha.",
                CompatibilityReasons = "Fecha de emisión referenciada diferente",
                ResolvedByUserId = userId, ResolvedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        return new SeedResult(note.Id, original.Id, reference.Id, originalKey);
    }

    private static ElectronicDocument MakeDocument(string key, ElectronicDocumentType type, string userId) => new()
    {
        Clave = key, NumeroConsecutivo = "00500001010000051684",
        DocumentType = type, Direction = ElectronicDocumentDirection.Received,
        FechaEmision = IssuedAt, IssuerIdentificationType = "02", IssuerIdentification = "3101157776",
        ReceiverIdentificationType = "02", ReceiverIdentification = "3102959852",
        CurrencyCode = "CRC", ExchangeRate = 1m, TotalDocument = 152631.36m,
        OriginalXml = [1, 2, 3], ImportedByUserId = userId, ImportedAtUtc = DateTime.UtcNow
    };

    private sealed record SeedResult(int NoteId, int OriginalId, int ReferenceId, string OriginalKey);
}
