using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class FiscalReferenceExceptionalLinkTests
{
    private const string Key = "50602102600310115777600500001010000051684120639723";
    private static readonly DateTimeOffset IssuedAt =
        new(2026, 10, 2, 13, 14, 44, TimeSpan.FromHours(-6));

    [Fact]
    public async Task AuthorizedDateMismatch_LinksWithoutApprovalOrEconomicChanges()
    {
        await using var db = CreateDb();
        var (note, original, reference) = Seed(db);
        Authorize(db, reference, original);
        var total = note.TotalDocument;
        var xml = note.OriginalXml.ToArray();

        await new ElectronicDocumentReferenceLinkService(db).LinkAsync(reference.Id, original.Id);

        Assert.Equal(original.Id, reference.RelatedElectronicDocumentId);
        Assert.Equal(ElectronicDocumentAdjustmentStatus.PendingReview, note.AdjustmentStatus);
        Assert.Equal(total, note.TotalDocument);
        Assert.Equal(xml, note.OriginalXml);
        Assert.Single(db.ElectronicDocumentReferenceResolutions);
    }

    [Fact]
    public async Task DateMismatchWithoutAuthorization_IsRejected()
    {
        await using var db = CreateDb();
        var (_, original, reference) = Seed(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ElectronicDocumentReferenceLinkService(db).LinkAsync(reference.Id, original.Id));
        Assert.Null(reference.RelatedElectronicDocumentId);
    }

    [Fact]
    public async Task AuthorizedDateMismatchButCurrencyChanged_IsRejected()
    {
        await using var db = CreateDb();
        var (_, original, reference) = Seed(db);
        Authorize(db, reference, original);
        original.CurrencyCode = "USD";
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ElectronicDocumentReferenceLinkService(db).LinkAsync(reference.Id, original.Id));
        Assert.Null(reference.RelatedElectronicDocumentId);
    }

    [Fact]
    public async Task AuthorizedDateMismatchButIssuerChanged_IsRejected()
    {
        await using var db = CreateDb();
        var (_, original, reference) = Seed(db);
        Authorize(db, reference, original);
        original.IssuerIdentification = "999999999";
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ElectronicDocumentReferenceLinkService(db).LinkAsync(reference.Id, original.Id));
        Assert.Null(reference.RelatedElectronicDocumentId);
    }

    [Fact]
    public async Task AuthorizationForDifferentOriginal_IsRejected()
    {
        await using var db = CreateDb();
        var (_, original, reference) = Seed(db);
        var other = NewDocument(3, ElectronicDocumentType.Invoice,
            "50602102600310115777600500001010000051684120639724");
        db.ElectronicDocuments.Add(other);
        db.SaveChanges();
        Authorize(db, reference, original, originalIdOverride: other.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ElectronicDocumentReferenceLinkService(db).LinkAsync(reference.Id, original.Id));
        Assert.Null(reference.RelatedElectronicDocumentId);
    }

    [Fact]
    public async Task AuthorizationDoesNotPermitConsecutiveNumberInsteadOfExactClave()
    {
        await using var db = CreateDb();
        var (_, original, reference) = Seed(db);
        Authorize(db, reference, original);
        reference.ReferenceNumber = original.NumeroConsecutivo;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ElectronicDocumentReferenceLinkService(db).LinkAsync(reference.Id, original.Id));
        Assert.Null(reference.RelatedElectronicDocumentId);
    }

    [Fact]
    public async Task ApprovedNoteCannotLinkEvenWithAuthorization()
    {
        await using var db = CreateDb();
        var (note, original, reference) = Seed(db);
        Authorize(db, reference, original);
        note.AdjustmentStatus = ElectronicDocumentAdjustmentStatus.Approved;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ElectronicDocumentReferenceLinkService(db).LinkAsync(reference.Id, original.Id));
        Assert.Null(reference.RelatedElectronicDocumentId);
    }

    private static RevestikDbContext CreateDb() => new(
        new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"FiscalExceptionalLinks-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static (ElectronicDocument Note, ElectronicDocument Original, ElectronicDocumentReference Reference)
        Seed(RevestikDbContext db)
    {
        db.Users.Add(new ApplicationUser { Id = "accountant", UserName = "accountant", IsActive = true });
        var original = NewDocument(1, ElectronicDocumentType.Invoice, Key);
        original.NumeroConsecutivo = "00500001010000051684";
        var note = NewDocument(2, ElectronicDocumentType.CreditNote,
            "50602102600310115777600500001030000051684120639723");
        note.AdjustmentStatus = ElectronicDocumentAdjustmentStatus.PendingReview;
        note.TotalDocument = 142054.56m;
        var reference = new ElectronicDocumentReference
        {
            Sequence = 1, ReferencedDocumentTypeCode = "01", ReferenceNumber = Key,
            ReferencedIssueDate = IssuedAt.AddDays(3), ReferenceCode = "06", Reason = "Devolución"
        };
        note.References.Add(reference);
        db.ElectronicDocuments.AddRange(original, note);
        db.SaveChanges();
        return (note, original, reference);
    }

    private static ElectronicDocument NewDocument(int id, ElectronicDocumentType type, string key) => new()
    {
        Id = id, Clave = key, DocumentType = type, Direction = ElectronicDocumentDirection.Received,
        FechaEmision = IssuedAt, IssuerIdentificationType = "02", IssuerIdentification = "3101157776",
        ReceiverIdentificationType = "02", ReceiverIdentification = "3102959852", CurrencyCode = "CRC",
        OriginalXml = [1, 2, 3], TotalDocument = 152631.36m
    };

    private static void Authorize(RevestikDbContext db, ElectronicDocumentReference reference,
        ElectronicDocument original, int? originalIdOverride = null)
    {
        db.ElectronicDocumentReferenceResolutions.Add(new ElectronicDocumentReferenceResolution
        {
            ElectronicDocumentReferenceId = reference.Id,
            OriginalDocumentId = originalIdOverride ?? original.Id,
            ResolutionType = "DateExceptionAuthorized",
            Justification = "La clave e identidades coinciden, la fecha está equivocada.",
            CompatibilityReasons = "Fecha de emisión referenciada diferente",
            ResolvedByUserId = "accountant",
            ResolvedAtUtc = DateTime.UtcNow
        });
        db.SaveChanges();
    }
}
