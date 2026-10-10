using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class FiscalAnalyticsServiceTests
{
    [Fact]
    public async Task GetAsync_GroupsByTypeAndCurrency_WithoutTreatingNotesAsOrdinaryPurchases()
    {
        await using var db = CreateDb();
        db.ElectronicDocuments.AddRange(
            Document("01", ElectronicDocumentType.Invoice, "CRC", 100, 1),
            Document("02", ElectronicDocumentType.CreditNote, "CRC", 30, null),
            Document("03", ElectronicDocumentType.ElectronicPaymentReceipt, "USD", 20, null));
        await db.SaveChangesAsync();
        var result = await new FiscalAnalyticsService(db).GetAsync(
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));
        Assert.Equal(3, result.Rows.Count);
        Assert.Contains(result.Rows, x => x.DocumentType == ElectronicDocumentType.Invoice &&
            x.LinkedPurchaseCount == 1 && x.EvidenceTotal == 100);
        Assert.Contains(result.Rows, x => x.DocumentType == ElectronicDocumentType.CreditNote && x.EvidenceTotal == 30);
        Assert.Contains(result.Rows, x => x.DocumentType == ElectronicDocumentType.ElectronicPaymentReceipt && x.CurrencyCode == "USD");
        Assert.Empty(db.Purchases);
        Assert.Empty(db.Expenses);
        Assert.Empty(db.InventoryMovements);
    }

    [Fact]
    public async Task GetAsync_ExcludesDocumentsOutsideRange()
    {
        await using var db = CreateDb();
        var document = Document("04", ElectronicDocumentType.Invoice, "CRC", 50, null);
        document.FechaEmision = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.FromHours(-6));
        db.ElectronicDocuments.Add(document);
        await db.SaveChangesAsync();
        var result = await new FiscalAnalyticsService(db).GetAsync(
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));
        Assert.Empty(result.Rows);
    }

    [Fact]
    public async Task GetAsync_RejectsInvalidRange()
    {
        await using var db = CreateDb();
        await Assert.ThrowsAsync<ArgumentException>(() => new FiscalAnalyticsService(db).GetAsync(
            new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 1)));
    }

    private static RevestikDbContext CreateDb() => new(
        new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"FiscalAnalytics-{Guid.NewGuid()}").Options);

    private static ElectronicDocument Document(string clave, ElectronicDocumentType type,
        string currency, decimal total, int? purchaseId) => new()
    {
        Clave = clave, DocumentType = type, CurrencyCode = currency, TotalDocument = total,
        PurchaseId = purchaseId, ProcessingStatus = ElectronicDocumentProcessingStatus.Pending,
        FechaEmision = new DateTimeOffset(2026, 10, 9, 10, 0, 0, TimeSpan.FromHours(-6))
    };
}
