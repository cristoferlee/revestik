using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

/// <summary>
/// Diagnostic regression: intentionally fails if a stale DbContext can overwrite
/// a reference already linked by another DbContext. Uses an isolated SQL Server
/// Testcontainers database supplied by the existing integration fixture.
/// </summary>
public sealed class FiscalReferenceConcurrencySqlTests(
    SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private static readonly DateTimeOffset IssuedAt =
        new(2026, 10, 2, 10, 0, 0, TimeSpan.FromHours(-6));

    [Fact]
    public async Task TwoStaleContexts_CannotOverwriteFirstReferenceLink()
    {
        int refId, firstId, secondId;
        await using (var seed = fixture.CreateDbContext())
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString("N"),
                UserName = $"fiscal-{Guid.NewGuid():N}@example.test",
                NormalizedUserName = $"FISCAL-{Guid.NewGuid():N}@EXAMPLE.TEST"
            };
            seed.Users.Add(user);
            var first = MakeDocument("50602102600310115777600500001010000051684120639723", ElectronicDocumentType.Invoice, user.Id);
            var second = MakeDocument("50602102600310115777600500001010000051684120639724", ElectronicDocumentType.Invoice, user.Id);
            var note = MakeDocument("50602102600310115777600500001030000051684120639723", ElectronicDocumentType.CreditNote, user.Id);
            first.NumeroConsecutivo = second.NumeroConsecutivo = "00500001010000051684";
            note.AdjustmentStatus = ElectronicDocumentAdjustmentStatus.PendingReview;
            var reference = new ElectronicDocumentReference
            {
                Sequence = 1,
                ReferencedDocumentTypeCode = "01",
                ReferenceNumber = first.NumeroConsecutivo,
                ReferencedIssueDate = IssuedAt,
                ReferenceCode = "01",
                Reason = "Prueba de concurrencia"
            };
            note.References.Add(reference);
            seed.ElectronicDocuments.AddRange(first, second, note);
            await seed.SaveChangesAsync();
            (refId, firstId, secondId) = (reference.Id, first.Id, second.Id);
        }

        // Force both independent contexts to cache the same unlinked reference.
        await using var contextA = fixture.CreateDbContext();
        await using var contextB = fixture.CreateDbContext();
        await contextA.Set<ElectronicDocumentReference>().Include(x => x.ElectronicDocument)
            .SingleAsync(x => x.Id == refId);
        await contextB.Set<ElectronicDocumentReference>().Include(x => x.ElectronicDocument)
            .SingleAsync(x => x.Id == refId);

        await new ElectronicDocumentReferenceLinkService(contextA).LinkAsync(refId, firstId);
        // Re-linking the same destination is idempotent.
        await new ElectronicDocumentReferenceLinkService(contextA).LinkAsync(refId, firstId);

        // The second request must fail instead of silently replacing A's link.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ElectronicDocumentReferenceLinkService(contextB).LinkAsync(refId, secondId));

        await using var verify = fixture.CreateDbContext();
        var actual = await verify.Set<ElectronicDocumentReference>().AsNoTracking()
            .Where(x => x.Id == refId)
            .Select(x => x.RelatedElectronicDocumentId)
            .SingleAsync();
        Assert.Equal(firstId, actual);
    }

    private static ElectronicDocument MakeDocument(string key, ElectronicDocumentType type, string userId) => new()
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
        TotalDocument = 100m,
        ProcessingStatus = ElectronicDocumentProcessingStatus.Pending,
        ImportedByUserId = userId,
        ImportedAtUtc = DateTime.UtcNow
    };
}
