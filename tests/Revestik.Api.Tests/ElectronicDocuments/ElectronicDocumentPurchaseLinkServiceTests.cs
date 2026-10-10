using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Shared.ElectronicDocuments;
using Revestik.Shared.Purchases;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class ElectronicDocumentPurchaseLinkServiceTests
{
    [Fact]
    public async Task Link_CompatibleDocument_PersistsOnlyPurchaseRelation()
    {
        await using var db = CreateDb();
        var (document, purchase) = await SeedAsync(db);

        await new ElectronicDocumentPurchaseLinkService(db).LinkAsync(document.Id, purchase.Id);

        Assert.Equal(purchase.Id, (await db.ElectronicDocuments.SingleAsync()).PurchaseId);
        Assert.Single(db.Purchases);
        Assert.Empty(db.Expenses);
        Assert.Empty(db.PurchasePayments);
        Assert.Empty(db.InventoryMovements);
    }

    [Fact]
    public async Task Link_DifferentSupplier_DoesNotMutateDocument()
    {
        await using var db = CreateDb();
        var (document, purchase) = await SeedAsync(db);
        document.IssuerIdentification = "3101999999";
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ElectronicDocumentPurchaseLinkService(db).LinkAsync(document.Id, purchase.Id));
        Assert.Null(document.PurchaseId);
    }

    [Fact]
    public async Task Link_DifferentCurrency_IsRejected()
    {
        await using var db = CreateDb();
        var (document, purchase) = await SeedAsync(db);
        purchase.Currency = PurchaseCurrency.USD;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ElectronicDocumentPurchaseLinkService(db).LinkAsync(document.Id, purchase.Id));
        Assert.Null(document.PurchaseId);
    }

    [Fact]
    public async Task Link_DoesNotAllowSamePurchaseOnAnotherDocument()
    {
        await using var db = CreateDb();
        var (document, purchase) = await SeedAsync(db);
        var other = new ElectronicDocument
        {
            Clave = new string('2', 50), DocumentType = ElectronicDocumentType.Invoice,
            Direction = ElectronicDocumentDirection.Received,
            IssuerIdentificationType = document.IssuerIdentificationType,
            IssuerIdentification = document.IssuerIdentification,
            CurrencyCode = document.CurrencyCode,
            ImportedByUserId = "tester"
        };
        db.ElectronicDocuments.Add(other);
        await db.SaveChangesAsync();

        var service = new ElectronicDocumentPurchaseLinkService(db);
        await service.LinkAsync(document.Id, purchase.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LinkAsync(other.Id, purchase.Id));
        Assert.Null(other.PurchaseId);
    }

    [Fact]
    public async Task Link_NotesAreNotOperationalPurchases()
    {
        await using var db = CreateDb();
        var (document, purchase) = await SeedAsync(db);
        document.DocumentType = ElectronicDocumentType.CreditNote;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ElectronicDocumentPurchaseLinkService(db).LinkAsync(document.Id, purchase.Id));
        Assert.Null(document.PurchaseId);
    }

    [Fact]
    public async Task Link_RepeatedWithSameIds_IsIdempotentAndCreatesNoEconomicRows()
    {
        await using var db = CreateDb();
        var (document, purchase) = await SeedAsync(db);
        var service = new ElectronicDocumentPurchaseLinkService(db);

        await service.LinkAsync(document.Id, purchase.Id);
        await service.LinkAsync(document.Id, purchase.Id);

        Assert.Equal(purchase.Id, (await db.ElectronicDocuments.SingleAsync()).PurchaseId);
        Assert.Single(db.Purchases);
        Assert.Single(db.Suppliers);
        Assert.Empty(db.PurchasePayments);
        Assert.Empty(db.InventoryMovements);
        Assert.Empty(db.InventoryCostLayers);
        Assert.Empty(db.Expenses);
    }

    [Fact]
    public async Task Link_AlreadyLinkedDocument_CannotChangeToAnotherPurchase()
    {
        await using var db = CreateDb();
        var (document, purchase) = await SeedAsync(db);
        var secondPurchase = new Purchase
        {
            SupplierId = purchase.SupplierId,
            PurchaseDate = new DateOnly(2026, 10, 2),
            Currency = PurchaseCurrency.CRC,
            CreatedByUserId = "tester"
        };
        db.Purchases.Add(secondPurchase);
        await db.SaveChangesAsync();

        var service = new ElectronicDocumentPurchaseLinkService(db);
        await service.LinkAsync(document.Id, purchase.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.LinkAsync(document.Id, secondPurchase.Id));

        Assert.Equal(purchase.Id, document.PurchaseId);
        Assert.Equal(2, await db.Purchases.CountAsync());
        Assert.Empty(db.InventoryMovements);
        Assert.Empty(db.PurchasePayments);
    }

    [Theory]
    [InlineData(ElectronicDocumentType.CreditNote)]
    [InlineData(ElectronicDocumentType.DebitNote)]
    [InlineData(ElectronicDocumentType.ElectronicPaymentReceipt)]
    public async Task Link_NonPurchaseFiscalEvidence_CannotCreatePurchaseRelationship(
        ElectronicDocumentType type)
    {
        await using var db = CreateDb();
        var (document, purchase) = await SeedAsync(db);
        document.DocumentType = type;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ElectronicDocumentPurchaseLinkService(db).LinkAsync(document.Id, purchase.Id));

        Assert.Null(document.PurchaseId);
        Assert.Single(db.Purchases);
        Assert.Empty(db.PurchasePayments);
        Assert.Empty(db.InventoryMovements);
        Assert.Empty(db.Expenses);
    }

    [Fact]
    public async Task Link_DoesNotAlterOriginalPurchaseOrFiscalValues()
    {
        await using var db = CreateDb();
        var (document, purchase) = await SeedAsync(db);
        document.TotalDocument = 130m;
        purchase.Notes = "Compra original";
        await db.SaveChangesAsync();

        await new ElectronicDocumentPurchaseLinkService(db).LinkAsync(document.Id, purchase.Id);
        db.ChangeTracker.Clear();

        var savedDocument = await db.ElectronicDocuments.SingleAsync();
        var savedPurchase = await db.Purchases.SingleAsync();
        Assert.Equal(130m, savedDocument.TotalDocument);
        Assert.Equal("Compra original", savedPurchase.Notes);
        Assert.Equal(purchase.Id, savedDocument.PurchaseId);
        Assert.Empty(db.PurchasePayments);
        Assert.Empty(db.InventoryMovements);
        Assert.Empty(db.InventoryCostLayers);
    }

    private static RevestikDbContext CreateDb() => new(
        new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"PurchaseLink-{Guid.NewGuid()}").Options);

    private static async Task<(ElectronicDocument Document, Purchase Purchase)> SeedAsync(RevestikDbContext db)
    {
        var supplier = new Supplier
        {
            Name = "Proveedor", IdentificationType = "02",
            IdentificationNumber = "3101157776", IsActive = true
        };
        var purchase = new Purchase
        {
            Supplier = supplier, PurchaseDate = new DateOnly(2026, 10, 1),
            Currency = PurchaseCurrency.CRC, CreatedByUserId = "tester"
        };
        var document = new ElectronicDocument
        {
            Clave = new string('1', 50), DocumentType = ElectronicDocumentType.Invoice,
            Direction = ElectronicDocumentDirection.Received,
            IssuerIdentificationType = "02", IssuerIdentification = "3101157776",
            ReceiverIdentificationType = "02", ReceiverIdentification = "3102959852",
            CurrencyCode = "CRC", ImportedByUserId = "tester"
        };
        db.Purchases.Add(purchase);
        db.ElectronicDocuments.Add(document);
        await db.SaveChangesAsync();
        return (document, purchase);
    }
}
