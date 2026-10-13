using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

/// <summary>
/// Verifies B1/B2 against SQL Server, using Revestik's existing shared
/// Testcontainers host and class-isolated integration database.
/// </summary>
public sealed class FiscalReferenceExceptionalLinkSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private static readonly DateTimeOffset IssuedAt =
        new(2026, 10, 2, 13, 14, 44, TimeSpan.FromHours(-6));

    [Fact]
    public async Task AuthorizedDateException_LinksInSql_WithoutApprovingOrMutatingXml()
    {
        var seed = await SeedAsync(authorize: true);

        await using (var linkDb = fixture.CreateDbContext())
            await new ElectronicDocumentReferenceLinkService(linkDb)
                .LinkAsync(seed.ReferenceId, seed.OriginalId);

        await using var verify = fixture.CreateDbContext();
        var reference = await verify.ElectronicDocumentReferences.AsNoTracking()
            .SingleAsync(x => x.Id == seed.ReferenceId);
        var note = await verify.ElectronicDocuments.AsNoTracking()
            .SingleAsync(x => x.Id == seed.NoteId);
        Assert.Equal(seed.OriginalId, reference.RelatedElectronicDocumentId);
        Assert.Equal(ElectronicDocumentAdjustmentStatus.PendingReview, note.AdjustmentStatus);
        Assert.Equal(142054.56m, note.TotalDocument);
        Assert.Equal(new byte[] { 1, 2, 3 }, note.OriginalXml);
        Assert.Equal(1, await verify.ElectronicDocumentReferenceResolutions.AsNoTracking()
            .CountAsync(x => x.ElectronicDocumentReferenceId == seed.ReferenceId));
    }

    [Fact]
    public async Task DateMismatchWithoutAuthorization_DoesNotCreateLinkInSql()
    {
        var seed = await SeedAsync(authorize: false);
        await using (var db = fixture.CreateDbContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new ElectronicDocumentReferenceLinkService(db)
                    .LinkAsync(seed.ReferenceId, seed.OriginalId));

        await using var verify = fixture.CreateDbContext();
        Assert.Null((await verify.ElectronicDocumentReferences.AsNoTracking()
            .SingleAsync(x => x.Id == seed.ReferenceId)).RelatedElectronicDocumentId);
    }

    [Fact]
    public async Task AuthorizedException_CannotBypassChangedCurrencyInSql()
    {
        var seed = await SeedAsync(authorize: true);
        await using (var editDb = fixture.CreateDbContext())
        {
            var original = await editDb.ElectronicDocuments.SingleAsync(x => x.Id == seed.OriginalId);
            original.CurrencyCode = "USD";
            await editDb.SaveChangesAsync();
        }
        await using (var linkDb = fixture.CreateDbContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new ElectronicDocumentReferenceLinkService(linkDb)
                    .LinkAsync(seed.ReferenceId, seed.OriginalId));

        await using var verify = fixture.CreateDbContext();
        Assert.Null((await verify.ElectronicDocumentReferences.AsNoTracking()
            .SingleAsync(x => x.Id == seed.ReferenceId)).RelatedElectronicDocumentId);
    }

    [Fact]
    public async Task TwoIndependentContexts_CannotOverwriteExceptionalLink()
    {
        var seed = await SeedAsync(authorize: true);
        await using var first = fixture.CreateDbContext();
        await using var second = fixture.CreateDbContext();

        // Both contexts observe the original, unlinked row before either writes.
        Assert.Null((await first.ElectronicDocumentReferences.AsNoTracking()
            .SingleAsync(x => x.Id == seed.ReferenceId)).RelatedElectronicDocumentId);
        Assert.Null((await second.ElectronicDocumentReferences.AsNoTracking()
            .SingleAsync(x => x.Id == seed.ReferenceId)).RelatedElectronicDocumentId);

        await new ElectronicDocumentReferenceLinkService(first)
            .LinkAsync(seed.ReferenceId, seed.OriginalId);
        // Repeating the same link is idempotent, never a replacement.
        await new ElectronicDocumentReferenceLinkService(second)
            .LinkAsync(seed.ReferenceId, seed.OriginalId);

        await using var verify = fixture.CreateDbContext();
        Assert.Equal(seed.OriginalId, (await verify.ElectronicDocumentReferences.AsNoTracking()
            .SingleAsync(x => x.Id == seed.ReferenceId)).RelatedElectronicDocumentId);
        Assert.Equal(ElectronicDocumentAdjustmentStatus.PendingReview,
            (await verify.ElectronicDocuments.AsNoTracking()
                .SingleAsync(x => x.Id == seed.NoteId)).AdjustmentStatus);
    }

    private async Task<(int ReferenceId, int OriginalId, int NoteId)> SeedAsync(bool authorize)
    {
        await using var db = fixture.CreateDbContext();
        var unique = Guid.NewGuid().ToString("N");
        var userId = unique;
        // Hacienda-style test Claves are exactly 50 digits, and unique per test.
        var suffix = Convert.ToUInt32(unique[..8], 16).ToString("D10");
        var originalKey = "5060210260031011577760050000101000005168" + suffix;
        var noteKey = "5060210260031011577760050000103000005168" + suffix;
        db.Users.Add(new ApplicationUser
        {
            Id = userId, UserName = $"b2-{unique}@example.test",
            NormalizedUserName = $"B2-{unique.ToUpperInvariant()}@EXAMPLE.TEST",
            IsActive = true
        });

        var original = MakeDocument(originalKey, ElectronicDocumentType.Invoice, userId);
        var note = MakeDocument(noteKey, ElectronicDocumentType.CreditNote, userId);
        note.AdjustmentStatus = ElectronicDocumentAdjustmentStatus.PendingReview;
        note.TotalDocument = 142054.56m;
        var reference = new ElectronicDocumentReference
        {
            Sequence = 1, ReferencedDocumentTypeCode = "01",
            ReferenceNumber = originalKey, ReferencedIssueDate = IssuedAt.AddDays(3),
            ReferenceCode = "06", Reason = "Devolución de mercancía"
        };
        note.References.Add(reference);
        db.ElectronicDocuments.AddRange(original, note);
        await db.SaveChangesAsync();

        if (authorize)
        {
            db.ElectronicDocumentReferenceResolutions.Add(new ElectronicDocumentReferenceResolution
            {
                ElectronicDocumentReferenceId = reference.Id,
                OriginalDocumentId = original.Id,
                ResolutionType = "DateExceptionAuthorized",
                Justification = "Comprobación manual: factura y clave coinciden, fecha incorrecta.",
                CompatibilityReasons = "Fecha de emisión referenciada diferente",
                ResolvedByUserId = userId,
                ResolvedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        return (reference.Id, original.Id, note.Id);
    }

    private static ElectronicDocument MakeDocument(
        string key, ElectronicDocumentType type, string userId) => new()
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
        TotalDocument = 152631.36m,
        OriginalXml = [1, 2, 3],
        ImportedByUserId = userId,
        ImportedAtUtc = DateTime.UtcNow
    };
}
