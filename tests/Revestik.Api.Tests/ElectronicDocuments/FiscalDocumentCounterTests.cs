using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class FiscalDocumentCounterTests
{
    [Fact]
    public async Task Counter_UsesSignedNotesAndRetainsUnapprovedNotesAsExcluded()
    {
        await using var db = CreateDb();
        db.ElectronicDocuments.AddRange(
            Document(1, ElectronicDocumentType.Invoice, 37100m),
            Document(2, ElectronicDocumentType.CreditNote, 37100m,
                ElectronicDocumentAdjustmentStatus.Approved),
            Document(3, ElectronicDocumentType.DebitNote, 120m,
                ElectronicDocumentAdjustmentStatus.Applied),
            Document(4, ElectronicDocumentType.CreditNote, 99999m,
                ElectronicDocumentAdjustmentStatus.PendingReview),
            Document(5, ElectronicDocumentType.ElectronicPaymentReceipt, 37100m));
        await db.SaveChangesAsync();
        var result = await new ElectronicDocumentPeriodSummaryService(db)
            .GetDocumentCounterAsync(new ElectronicDocumentSummaryRequest(), CancellationToken.None);
        var crc = Assert.Single(result.Totals);
        Assert.Equal(37100m, crc.InvoiceTotal);
        Assert.Equal(37100m, crc.CreditNoteTotal);
        Assert.Equal(120m, crc.DebitNoteTotal);
        Assert.Equal(120m, crc.NetTotal);
        Assert.Equal(2, result.Documents.Count(x => !x.IsIncluded));
    }

    [Fact]
    public async Task Counter_DistinguishesOriginalCalendarDateAndCurrencies()
    {
        await using var db = CreateDb();
        var crc = Document(1, ElectronicDocumentType.Invoice, 50m);
        crc.FechaEmision = new DateTimeOffset(2026, 10, 1, 0, 10, 0, TimeSpan.FromHours(-6));
        var usd = Document(2, ElectronicDocumentType.Invoice, 20m);
        usd.CurrencyCode = "USD";
        db.ElectronicDocuments.AddRange(crc, usd);
        await db.SaveChangesAsync();
        var result = await new ElectronicDocumentPeriodSummaryService(db)
            .GetDocumentCounterAsync(new ElectronicDocumentSummaryRequest
            { DateFrom = new DateOnly(2026, 10, 1), DateTo = new DateOnly(2026, 10, 1) },
            CancellationToken.None);
        Assert.Equal(2, result.Totals.Count);
        Assert.Equal(50m, result.Totals.Single(x => x.CurrencyCode == "CRC").NetTotal);
        Assert.Equal(20m, result.Totals.Single(x => x.CurrencyCode == "USD").NetTotal);
    }

    [Fact]
    public async Task Counter_PreservesCalendarDayAcrossExtremeOffsetsAndExcludesAdjacentDays()
    {
        await using var db = CreateDb();
        var beginning = Document(1, ElectronicDocumentType.Invoice, 11m);
        beginning.FechaEmision = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(14));
        var ending = Document(2, ElectronicDocumentType.Invoice, 13m);
        ending.FechaEmision = new DateTimeOffset(2026, 10, 1, 23, 59, 59, TimeSpan.FromHours(-14));
        var previous = Document(3, ElectronicDocumentType.Invoice, 17m);
        previous.FechaEmision = new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.FromHours(-14));
        var next = Document(4, ElectronicDocumentType.Invoice, 19m);
        next.FechaEmision = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.FromHours(14));
        db.ElectronicDocuments.AddRange(beginning, ending, previous, next);
        await db.SaveChangesAsync();

        var result = await new ElectronicDocumentPeriodSummaryService(db)
            .GetDocumentCounterAsync(new ElectronicDocumentSummaryRequest
            {
                DateFrom = new DateOnly(2026, 10, 1),
                DateTo = new DateOnly(2026, 10, 1)
            }, CancellationToken.None);

        Assert.Equal(24m, Assert.Single(result.Totals).NetTotal);
        Assert.Equal(new[] { 1, 2 }, result.Documents.Select(x => x.DocumentId).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task Counter_UnboundedRangeStillIncludesAllDocumentDates()
    {
        await using var db = CreateDb();
        var old = Document(1, ElectronicDocumentType.Invoice, 40m);
        old.FechaEmision = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.FromHours(14));
        var recent = Document(2, ElectronicDocumentType.Invoice, 60m);
        recent.FechaEmision = new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.FromHours(-6));
        db.ElectronicDocuments.AddRange(old, recent);
        await db.SaveChangesAsync();

        var result = await new ElectronicDocumentPeriodSummaryService(db)
            .GetDocumentCounterAsync(new ElectronicDocumentSummaryRequest(), CancellationToken.None);

        Assert.Equal(100m, Assert.Single(result.Totals).NetTotal);
        Assert.Equal(2, result.Documents.Count);
    }

    private static RevestikDbContext CreateDb() => new(
        new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase("FiscalDocumentCounter-" + Guid.NewGuid()).Options);

    private static ElectronicDocument Document(int id, ElectronicDocumentType type, decimal total,
        ElectronicDocumentAdjustmentStatus? status = null) => new()
    {
        Id = id,
        Clave = $"5060110260031011577760050000101000005{id:000000}12345678",
        DocumentType = type,
        AdjustmentStatus = status,
        Direction = ElectronicDocumentDirection.Received,
        FechaEmision = new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.FromHours(-6)),
        CurrencyCode = "CRC",
        TotalDocument = total,
        ImportedByUserId = "test", OriginalXml = [1],
        NumeroConsecutivo = $"00100001010000{id:00000}"
    };
}
