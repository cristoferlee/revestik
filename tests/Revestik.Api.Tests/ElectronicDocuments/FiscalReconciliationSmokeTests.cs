using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Shared.ElectronicDocuments;
using Revestik.Shared.Purchases;

namespace Revestik.Api.Tests.ElectronicDocuments;

/// <summary>
/// An isolated, non-production smoke path across fiscal review and operational linkage.
/// All persistence is EF Core InMemory; no Gmail, live API, or production SQL is touched.
/// </summary>
public sealed class FiscalReconciliationSmokeTests
{
    [Fact]
    public async Task Smoke_ReviewLinkReviewRepeat_PreservesOperationalState()
    {
        await using var db = CreateDb();
        var (document, purchase) = await SeedAsync(db);
        var reconciliation = new ElectronicDocumentPurchaseReconciliationService(db);
        var linking = new ElectronicDocumentPurchaseLinkService(db);

        var before = await reconciliation.ReviewAsync(document.Id, purchase.Id);
        Assert.True(before.CanLinkUnderCurrentRules);
        Assert.Null(document.PurchaseId);

        await linking.LinkAsync(document.Id, purchase.Id);
        await linking.LinkAsync(document.Id, purchase.Id); // retry is idempotent

        db.ChangeTracker.Clear();
        var after = await reconciliation.ReviewAsync(document.Id, purchase.Id);
        Assert.True(after.CanLinkUnderCurrentRules);
        Assert.Equal(before.FiscalTotal, after.FiscalTotal);
        Assert.Equal(before.FiscalNetSale, after.FiscalNetSale);
        var persisted = await db.ElectronicDocuments.SingleAsync();
        Assert.Equal(purchase.Id, persisted.PurchaseId);
        Assert.Equal(113m, persisted.TotalDocument);
        Assert.Equal(100m, persisted.TotalNetSale);
        Assert.Equal("Compra existente", (await db.Purchases.SingleAsync()).Notes);
        await AssertEconomicStateAsync(db, expectedPurchases: 1);
    }

    [Fact]
    public async Task Smoke_MismatchedIssuerCannotLinkOrChangeEconomicState()
    {
        await using var db = CreateDb();
        var (document, purchase) = await SeedAsync(db);
        document.IssuerIdentification = "3101999999";
        await db.SaveChangesAsync();

        var review = await new ElectronicDocumentPurchaseReconciliationService(db)
            .ReviewAsync(document.Id, purchase.Id);
        Assert.False(review.CanLinkUnderCurrentRules);
        Assert.Contains(review.Warnings, x => x.Code == "SupplierMismatch");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ElectronicDocumentPurchaseLinkService(db).LinkAsync(document.Id, purchase.Id));

        Assert.Null((await db.ElectronicDocuments.SingleAsync()).PurchaseId);
        await AssertEconomicStateAsync(db, expectedPurchases: 1);
    }

    [Fact]
    public async Task Smoke_MismatchedCurrencyCannotLinkOrChangeEconomicState()
    {
        await using var db = CreateDb();
        var (document, purchase) = await SeedAsync(db);
        purchase.Currency = PurchaseCurrency.USD;
        await db.SaveChangesAsync();

        var review = await new ElectronicDocumentPurchaseReconciliationService(db)
            .ReviewAsync(document.Id, purchase.Id);
        Assert.False(review.CanLinkUnderCurrentRules);
        Assert.Contains(review.Warnings, x => x.Code == "CurrencyMismatch");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ElectronicDocumentPurchaseLinkService(db).LinkAsync(document.Id, purchase.Id));
        Assert.Null(document.PurchaseId);
        await AssertEconomicStateAsync(db, expectedPurchases: 1);
    }

    [Theory]
    [InlineData(ElectronicDocumentType.CreditNote)]
    [InlineData(ElectronicDocumentType.DebitNote)]
    [InlineData(ElectronicDocumentType.ElectronicPaymentReceipt)]
    public async Task Smoke_AdjustmentsAndReceiptsNeverLinkAsOperationalPurchases(
        ElectronicDocumentType type)
    {
        await using var db = CreateDb();
        var (document, purchase) = await SeedAsync(db);
        document.DocumentType = type;
        await db.SaveChangesAsync();

        var review = await new ElectronicDocumentPurchaseReconciliationService(db)
            .ReviewAsync(document.Id, purchase.Id);
        Assert.False(review.CanLinkUnderCurrentRules);
        Assert.Contains(review.Warnings, x => x.Code == "UnsupportedDocument");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ElectronicDocumentPurchaseLinkService(db).LinkAsync(document.Id, purchase.Id));
        Assert.Null(document.PurchaseId);
        await AssertEconomicStateAsync(db, expectedPurchases: 1);
    }

    [Fact]
    public async Task Smoke_TwoFiscalDocumentsCannotShareOnePurchase()
    {
        await using var db = CreateDb();
        var (document, purchase) = await SeedAsync(db);
        var second = CreateDocument(new string('2', 50));
        db.ElectronicDocuments.Add(second);
        await db.SaveChangesAsync();
        var linking = new ElectronicDocumentPurchaseLinkService(db);

        await linking.LinkAsync(document.Id, purchase.Id);
        var review = await new ElectronicDocumentPurchaseReconciliationService(db)
            .ReviewAsync(second.Id, purchase.Id);
        Assert.False(review.CanLinkUnderCurrentRules);
        Assert.Contains(review.Warnings, x => x.Code == "PurchaseAlreadyLinked");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            linking.LinkAsync(second.Id, purchase.Id));
        Assert.Null(second.PurchaseId);
        Assert.Equal(purchase.Id, document.PurchaseId);
        await AssertEconomicStateAsync(db, expectedPurchases: 1);
    }

    [Fact]
    public async Task Smoke_ReconciliationIsReadOnlyEvenWithAdvisoryDifferences()
    {
        await using var db = CreateDb();
        var (document, purchase) = await SeedAsync(db);
        document.FechaEmision = new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.FromHours(-6));
        document.TotalNetSale = 250m;
        await db.SaveChangesAsync();

        var review = await new ElectronicDocumentPurchaseReconciliationService(db)
            .ReviewAsync(document.Id, purchase.Id);
        Assert.True(review.CanLinkUnderCurrentRules);
        Assert.Contains(review.Warnings, x => x.Code == "DateDifference");
        Assert.Contains(review.Warnings, x => x.Code == "NoPurchaseLines");
        Assert.Null(document.PurchaseId);
        Assert.Equal("Compra existente", purchase.Notes);
        await AssertEconomicStateAsync(db, expectedPurchases: 1);
    }

    private static RevestikDbContext CreateDb() => new(
        new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"FiscalSmoke-{Guid.NewGuid()}").Options);

    private static ElectronicDocument CreateDocument(string clave) => new()
    {
        Clave = clave,
        DocumentType = ElectronicDocumentType.Invoice,
        Direction = ElectronicDocumentDirection.Received,
        IssuerIdentificationType = "02",
        IssuerIdentification = "3101157776",
        ReceiverIdentificationType = "02",
        ReceiverIdentification = "3102959852",
        CurrencyCode = "CRC",
        TotalNetSale = 100m,
        TotalDocument = 113m,
        FechaEmision = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(-6)),
        ImportedByUserId = "tester"
    };

    private static async Task<(ElectronicDocument Document, Purchase Purchase)> SeedAsync(RevestikDbContext db)
    {
        var supplier = new Supplier
        {
            Name = "Proveedor",
            IdentificationType = "02",
            IdentificationNumber = "3101157776",
            IsActive = true
        };
        var purchase = new Purchase
        {
            Supplier = supplier,
            PurchaseDate = new DateOnly(2026, 10, 1),
            Currency = PurchaseCurrency.CRC,
            Notes = "Compra existente",
            CreatedByUserId = "tester"
        };
        var document = CreateDocument(new string('1', 50));
        db.Purchases.Add(purchase);
        db.ElectronicDocuments.Add(document);
        await db.SaveChangesAsync();
        return (document, purchase);
    }

    private static async Task AssertEconomicStateAsync(RevestikDbContext db, int expectedPurchases)
    {
        Assert.Equal(expectedPurchases, await db.Purchases.CountAsync());
        Assert.Equal(1, await db.Suppliers.CountAsync());
        Assert.Empty(await db.PurchasePayments.ToListAsync());
        Assert.Empty(await db.Expenses.ToListAsync());
        Assert.Empty(await db.InventoryMovements.ToListAsync());
        Assert.Empty(await db.InventoryCostLayers.ToListAsync());
    }
}
