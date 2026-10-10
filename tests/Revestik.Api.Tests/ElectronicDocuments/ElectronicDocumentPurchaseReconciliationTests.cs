using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Shared.ElectronicDocuments;
using Revestik.Shared.Purchases;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class ElectronicDocumentPurchaseReconciliationTests
{
    [Fact]
    public async Task Review_MatchingEvidence_DoesNotChangeEconomicData()
    {
        await using var db = CreateDb();
        var (document, purchase) = await SeedAsync(db);
        var result = await new ElectronicDocumentPurchaseReconciliationService(db).ReviewAsync(document.Id, purchase.Id);
        Assert.True(result.CanLinkUnderCurrentRules);
        Assert.Contains(result.Warnings, x => x.Code == "NoPurchaseLines");
        Assert.Null((await db.ElectronicDocuments.SingleAsync()).PurchaseId);
        Assert.Empty(db.PurchasePayments);
        Assert.Empty(db.InventoryMovements);
        Assert.Empty(db.Expenses);
    }

    [Fact]
    public async Task Review_MismatchedSupplierAndCurrency_ReportsBlockingIssues()
    {
        await using var db = CreateDb();
        var (document, purchase) = await SeedAsync(db);
        document.IssuerIdentification = "999";
        purchase.Currency = PurchaseCurrency.USD;
        await db.SaveChangesAsync();
        var result = await new ElectronicDocumentPurchaseReconciliationService(db).ReviewAsync(document.Id, purchase.Id);
        Assert.False(result.CanLinkUnderCurrentRules);
        Assert.Contains(result.Warnings, x => x.Code == "SupplierMismatch");
        Assert.Contains(result.Warnings, x => x.Code == "CurrencyMismatch");
    }

    [Fact]
    public async Task Review_DifferentDateAndAmounts_AreAdvisory()
    {
        await using var db = CreateDb();
        var (document, purchase) = await SeedAsync(db);
        document.FechaEmision = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(-6));
        document.TotalNetSale = 250m;
        await db.SaveChangesAsync();
        var result = await new ElectronicDocumentPurchaseReconciliationService(db).ReviewAsync(document.Id, purchase.Id);
        Assert.True(result.CanLinkUnderCurrentRules);
        Assert.Contains(result.Warnings, x => x.Code == "DateDifference");
    }

    [Fact]
    public async Task Review_NotesAndPaymentReceipts_AreNotPurchases()
    {
        foreach (var type in new[] { ElectronicDocumentType.CreditNote, ElectronicDocumentType.DebitNote, ElectronicDocumentType.ElectronicPaymentReceipt })
        {
            await using var db = CreateDb();
            var (document, purchase) = await SeedAsync(db);
            document.DocumentType = type;
            await db.SaveChangesAsync();
            var result = await new ElectronicDocumentPurchaseReconciliationService(db).ReviewAsync(document.Id, purchase.Id);
            Assert.False(result.CanLinkUnderCurrentRules);
            Assert.Contains(result.Warnings, x => x.Code == "UnsupportedDocument");
        }
    }

    private static RevestikDbContext CreateDb() => new(
        new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"Reconciliation-{Guid.NewGuid()}").Options);

    private static async Task<(ElectronicDocument, Purchase)> SeedAsync(RevestikDbContext db)
    {
        var supplier = new Supplier { Name = "Proveedor", IdentificationType = "02", IdentificationNumber = "3101157776", IsActive = true };
        var purchase = new Purchase { Supplier = supplier, PurchaseDate = new DateOnly(2026, 10, 1), Currency = PurchaseCurrency.CRC, CreatedByUserId = "tester" };
        var document = new ElectronicDocument
        {
            Clave = new string('1', 50), DocumentType = ElectronicDocumentType.Invoice,
            Direction = ElectronicDocumentDirection.Received,
            IssuerIdentificationType = "02", IssuerIdentification = "3101157776",
            CurrencyCode = "CRC", TotalNetSale = 100m, TotalDocument = 113m,
            FechaEmision = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(-6)), ImportedByUserId = "tester"
        };
        db.Purchases.Add(purchase);
        db.ElectronicDocuments.Add(document);
        await db.SaveChangesAsync();
        return (document, purchase);
    }
}
