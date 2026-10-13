using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

// Service behavior only. Relational transaction, filtered index, and race tests require SQL Server.
public sealed class FiscalReferenceResolutionServiceTests
{
    private const string Key = "50602102600310115777600500001010000051684120639723";
    private const string UserId = "reviewer";
    private static readonly DateTimeOffset Date = new(2026, 10, 2, 13, 14, 44, TimeSpan.FromHours(-6));
    private static readonly FiscalDateExceptionRequest Request = new(1, "La clave fiscal coincide; el proveedor indicó una fecha distinta.");

    [Fact]
    public async Task DateOnlyException_RecordsHistory_WithoutLinkOrApproval()
    {
        await using var db = CreateDb();
        var (note, _, reference) = Seed(db);
        var total = note.TotalDocument;
        var xml = note.OriginalXml.ToArray();
        var service = new FiscalReferenceResolutionService(db);
        var response = await service.AuthorizeDateExceptionAsync(reference.Id, Request, UserId, default);
        Assert.Equal(reference.Id, response.ReferenceId);
        Assert.Equal(UserId, response.ResolvedByUserId);
        Assert.Equal("DateExceptionAuthorized", response.ResolutionType);
        Assert.Single(await service.GetHistoryAsync(reference.Id, default));
        Assert.Null(reference.RelatedElectronicDocumentId);
        Assert.Equal(ElectronicDocumentAdjustmentStatus.PendingReview, note.AdjustmentStatus);
        Assert.Equal(total, note.TotalDocument);
        Assert.Equal(xml, note.OriginalXml);
    }

    [Fact]
    public async Task DuplicateAuthorization_IsBlocked()
    {
        await using var db = CreateDb();
        var (_, _, reference) = Seed(db);
        var service = new FiscalReferenceResolutionService(db);
        await service.AuthorizeDateExceptionAsync(reference.Id, Request, UserId, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AuthorizeDateExceptionAsync(reference.Id, Request, UserId, default));
        Assert.Single(await service.GetHistoryAsync(reference.Id, default));
    }

    [Fact]
    public async Task WrongIssuer_IsBlocked()
    {
        await using var db = CreateDb();
        var (_, original, reference) = Seed(db);
        original.IssuerIdentification = "9999999999";
        await db.SaveChangesAsync();
        await RejectAndAssertEmpty(db, reference.Id);
    }

    [Fact]
    public async Task WrongCurrency_IsBlocked()
    {
        await using var db = CreateDb();
        var (_, original, reference) = Seed(db);
        original.CurrencyCode = "USD";
        await db.SaveChangesAsync();
        await RejectAndAssertEmpty(db, reference.Id);
    }

    [Fact]
    public async Task InactiveUser_IsBlocked()
    {
        await using var db = CreateDb();
        var (_, _, reference) = Seed(db);
        (await db.Users.FindAsync(UserId))!.IsActive = false;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new FiscalReferenceResolutionService(db).AuthorizeDateExceptionAsync(reference.Id, Request, UserId, default));
        Assert.Empty(db.ElectronicDocumentReferenceResolutions);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    public async Task InvalidJustification_IsBlocked(string justification)
    {
        await using var db = CreateDb();
        var (_, _, reference) = Seed(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new FiscalReferenceResolutionService(db).AuthorizeDateExceptionAsync(
                reference.Id, new FiscalDateExceptionRequest(1, justification), UserId, default));
        Assert.Empty(db.ElectronicDocumentReferenceResolutions);
    }

    [Fact]
    public async Task ApprovedNote_IsBlocked()
    {
        await using var db = CreateDb();
        var (note, _, reference) = Seed(db);
        note.AdjustmentStatus = ElectronicDocumentAdjustmentStatus.Approved;
        await db.SaveChangesAsync();
        await RejectAndAssertEmpty(db, reference.Id);
    }

    [Fact]
    public async Task DocumentWithoutOriginalXml_IsBlocked()
    {
        await using var db = CreateDb();
        var (note, _, reference) = Seed(db);
        note.OriginalXml = [];
        await db.SaveChangesAsync();
        await RejectAndAssertEmpty(db, reference.Id);
    }

    [Fact]
    public async Task DateCompatibility_HasStableMachineReadableCode()
    {
        await using var db = CreateDb();
        var (note, original, reference) = Seed(db);
        var finding = Assert.Single(ElectronicDocumentReferenceCompatibilityEvaluator.EvaluateDetailed(note, reference, original));
        Assert.Equal(ElectronicDocumentReferenceCompatibilityEvaluator.CompatibilityIssue.IssueDate, finding.Code);
    }

    private static async Task RejectAndAssertEmpty(RevestikDbContext db, int referenceId)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new FiscalReferenceResolutionService(db).AuthorizeDateExceptionAsync(referenceId, Request, UserId, default));
        Assert.Empty(db.ElectronicDocumentReferenceResolutions);
    }

    private static RevestikDbContext CreateDb() => new(
        new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"FiscalDateResolution-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static (ElectronicDocument Note, ElectronicDocument Original, ElectronicDocumentReference Reference)
        Seed(RevestikDbContext db)
    {
        db.Users.Add(new ApplicationUser { Id = UserId, UserName = "reviewer", IsActive = true });
        var original = NewDocument(ElectronicDocumentType.Invoice, Key);
        original.Id = 1;
        var note = NewDocument(ElectronicDocumentType.CreditNote,
            "50602102600310115777600500001030000051684120639723");
        note.Id = 2; // Stable distinct key: original is Id 1; prevents EF InMemory graph collision.
        note.AdjustmentStatus = ElectronicDocumentAdjustmentStatus.PendingReview;
        note.TotalDocument = 142054.56m;
        var reference = new ElectronicDocumentReference
        {
            Sequence = 1,
            ReferencedDocumentTypeCode = "01",
            ReferenceNumber = Key,
            ReferencedIssueDate = Date.AddDays(3),
            ReferenceCode = "06",
            Reason = "Devolución"
        };
        note.References.Add(reference);
        db.ElectronicDocuments.AddRange(original, note);
        db.SaveChanges();
        return (note, original, reference);
    }

    private static ElectronicDocument NewDocument(ElectronicDocumentType kind, string key) => new()
    {
        Clave = key, DocumentType = kind, Direction = ElectronicDocumentDirection.Received,
        FechaEmision = Date, IssuerIdentificationType = "02", IssuerIdentification = "3101157776",
        ReceiverIdentificationType = "02", ReceiverIdentification = "3102959852",
        CurrencyCode = "CRC", OriginalXml = [1, 2, 3], TotalDocument = 152631.36m
    };
}
