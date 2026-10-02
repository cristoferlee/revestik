using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Inventory;
using Revestik.Shared.Inventory;
using Revestik.Shared.Purchases;

namespace Revestik.Api.Tests.Purchases;

public sealed class PurchaseInventoryReceiptServiceTests
{
    private const string UserId =
        "purchase-inventory-service-user";

    [Fact]
    public async Task ReceiveAsync_CreatesMovementLayerAndUpdatesProduct()
    {
        await using var dbContext = CreateDbContext();

        var line = await SeedPurchaseLineAsync(
            dbContext,
            quantity: 10m,
            requiresWholeUnits: true);

        var service =
            new InventoryPurchaseReceiptService(
                dbContext);

        await service.ReceiveAsync(
            line.Id,
            12500m,
            UserId,
            CancellationToken.None);

        var product = await dbContext.Products
            .SingleAsync(item =>
                item.Id == line.ProductId);

        Assert.Equal(10m, product.StockQuantity);
        Assert.Equal(12500m, product.CurrentCost);

        var movement = await dbContext.InventoryMovements
            .SingleAsync(item =>
                item.PurchaseLineId == line.Id);

        Assert.Equal(
            InventoryMovementType.Purchase,
            movement.Type);
        Assert.Equal(10m, movement.QuantityChange);
        Assert.Equal(0m, movement.StockBefore);
        Assert.Equal(10m, movement.StockAfter);
        Assert.Equal(12500m, movement.UnitCost);

        var layer = await dbContext.InventoryCostLayers
            .SingleAsync(item =>
                item.SourceMovementId == movement.Id);

        Assert.Equal(10m, layer.OriginalQuantity);
        Assert.Equal(10m, layer.RemainingQuantity);
        Assert.Equal(12500m, layer.UnitCost);
    }

    [Fact]
    public async Task ReceiveAsync_DoesNotRequireInitialStockMovement()
    {
        await using var dbContext = CreateDbContext();

        var line = await SeedPurchaseLineAsync(
            dbContext,
            quantity: 5m,
            requiresWholeUnits: true);

        Assert.False(
            await dbContext.InventoryMovements
                .AnyAsync(item =>
                    item.ProductId == line.ProductId));

        var service =
            new InventoryPurchaseReceiptService(
                dbContext);

        await service.ReceiveAsync(
            line.Id,
            8000m,
            UserId,
            CancellationToken.None);

        Assert.Equal(
            5m,
            (await dbContext.Products
                .SingleAsync(item =>
                    item.Id == line.ProductId))
                .StockQuantity);
    }

    [Fact]
    public async Task ReceiveAsync_WhenWholeUnitsRequired_RejectsFraction()
    {
        await using var dbContext = CreateDbContext();

        var line = await SeedPurchaseLineAsync(
            dbContext,
            quantity: 1.5m,
            requiresWholeUnits: true);

        var service =
            new InventoryPurchaseReceiptService(
                dbContext);

        await Assert.ThrowsAsync<
            InvalidInventoryOperationException>(
            () => service.ReceiveAsync(
                line.Id,
                8000m,
                UserId,
                CancellationToken.None));

        Assert.Empty(dbContext.InventoryMovements);
        Assert.Empty(dbContext.InventoryCostLayers);
    }

    [Fact]
    public async Task ReceiveAsync_WhenLineAlreadyApplied_RejectsSecondReceipt()
    {
        await using var dbContext = CreateDbContext();

        var line = await SeedPurchaseLineAsync(
            dbContext,
            quantity: 5m,
            requiresWholeUnits: true);

        var service =
            new InventoryPurchaseReceiptService(
                dbContext);

        await service.ReceiveAsync(
            line.Id,
            8000m,
            UserId,
            CancellationToken.None);

        await Assert.ThrowsAsync<
            PurchaseInventoryAlreadyAppliedException>(
            () => service.ReceiveAsync(
                line.Id,
                8000m,
                UserId,
                CancellationToken.None));
    }

    private static RevestikDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<RevestikDbContext>()
                .UseInMemoryDatabase(
                    $"PurchaseInventoryReceipt-{Guid.NewGuid()}")
                .Options;

        return new RevestikDbContext(options);
    }

    private static async Task<PurchaseLine>
        SeedPurchaseLineAsync(
            RevestikDbContext dbContext,
            decimal quantity,
            bool requiresWholeUnits)
    {
        var category = new ProductCategory
        {
            Name = "Pegamentos",
            CreatedAtUtc = DateTime.UtcNow
        };

        var unit = new UnitOfMeasure
        {
            Name = "Saco",
            Symbol = "saco",
            RequiresWholeQuantity =
                requiresWholeUnits,
            CreatedAtUtc = DateTime.UtcNow
        };

        var user = new ApplicationUser
        {
            Id = UserId,
            UserName =
                "purchase-inventory@example.com",
            NormalizedUserName =
                "PURCHASE-INVENTORY@EXAMPLE.COM",
            Email =
                "purchase-inventory@example.com",
            NormalizedEmail =
                "PURCHASE-INVENTORY@EXAMPLE.COM",
            EmailConfirmed = true,
            DisplayName = "Purchase Inventory User",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var supplier = new Supplier
        {
            Name = "Proveedor Test",
            ContactName = "Contacto",
            PhoneNumber = "88888888",
            Email = "proveedor@example.com",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.AddRange(
            category,
            unit,
            user,
            supplier);

        await dbContext.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            Name = "Producto compra",
            Description = "Producto de prueba.",
            CabysCode = "1234567890123",
            InventoryUnitId = unit.Id,
            CommercialUnitId = unit.Id,
            CommercialUnitsPerInventoryUnit = 1m,
            RequiresWholeInventoryUnits =
                requiresWholeUnits,
            SalePrice = 10000m,
            CurrentCost = 7000m,
            TaxRate = 13m,
            StockQuantity = 0m,
            MinimumStock = 0m,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var purchase = new Purchase
        {
            SupplierId = supplier.Id,
            PurchaseDate =
                DateOnly.FromDateTime(DateTime.Today),
            Currency = PurchaseCurrency.CRC,
            Notes = string.Empty,
            CreatedByUserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow
        };

        var line = new PurchaseLine
        {
            Purchase = purchase,
            ProductId = product.Id,
            Quantity = quantity,
            UnitCost = 7000m
        };

        purchase.Lines.Add(line);
        dbContext.Purchases.Add(purchase);
        await dbContext.SaveChangesAsync();

        return line;
    }
}