using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Inventory;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Tests.Inventory;

public sealed class InventorySaleReversalTests
{
    private const string UserId = "inventory-sale-reversal-user";
    private const int SaleId = 910001;
    private const string SaleNumber = "VEN-REV-001";

    [Fact]
    public async Task ReverseSaleStock_RestoresStockAndCreatesLinkedReversal()
    {
        await using var db = CreateDb();
        var product = await SeedInitializedProductAsync(
            db,
            quantity: 100m,
            unitCost: 7000m);

        var service = new InventoryService(db);

        await service.ConsumeSaleStockAsync(
            product.Id,
            80m,
            SaleId,
            SaleNumber,
            UserId,
            CancellationToken.None);

        var originalMovement = await db.InventoryMovements
            .SingleAsync(movement =>
                movement.SaleId == SaleId &&
                movement.Type == InventoryMovementType.Sale);

        var originalLayer = await db.InventoryCostLayers
            .SingleAsync(layer => layer.ProductId == product.Id);

        var layerId = originalLayer.Id;
        var sourceMovementId = originalLayer.SourceMovementId;
        var originalCreatedAtUtc = originalLayer.CreatedAtUtc;
        var originalCost = originalLayer.UnitCost;

        Assert.Equal(20m, product.StockQuantity);
        Assert.Equal(20m, originalLayer.RemainingQuantity);
        Assert.Single(
            await db.InventoryCostConsumptions
                .Where(item =>
                    item.InventoryMovementId == originalMovement.Id)
                .ToListAsync());

        await service.ReverseSaleStockAsync(
            SaleId,
            SaleNumber,
            UserId,
            CancellationToken.None);

        Assert.Equal(100m, product.StockQuantity);

        var restoredLayer = await db.InventoryCostLayers
            .SingleAsync(layer => layer.ProductId == product.Id);

        Assert.Equal(layerId, restoredLayer.Id);
        Assert.Equal(sourceMovementId, restoredLayer.SourceMovementId);
        Assert.Equal(originalCreatedAtUtc, restoredLayer.CreatedAtUtc);
        Assert.Equal(originalCost, restoredLayer.UnitCost);
        Assert.Equal(100m, restoredLayer.RemainingQuantity);

        Assert.Equal(
            1,
            await db.InventoryCostLayers.CountAsync(
                layer => layer.ProductId == product.Id));

        Assert.Equal(
            1,
            await db.InventoryCostConsumptions.CountAsync(
                item =>
                    item.InventoryMovementId == originalMovement.Id));

        var reversal = await db.InventoryMovements
            .SingleAsync(movement =>
                movement.Type == InventoryMovementType.SaleReversal);

        Assert.Equal(SaleId, reversal.SaleId);
        Assert.Equal(originalMovement.Id, reversal.ReversesInventoryMovementId);
        Assert.Equal(80m, reversal.QuantityChange);
        Assert.Equal(20m, reversal.StockBefore);
        Assert.Equal(100m, reversal.StockAfter);
    }

    [Fact]
    public async Task ReverseSaleStock_WhenSaleExceedsAvailableStock_RestoresOnlyPhysicalConsumption()
    {
        await using var db = CreateDb();
        var product = await SeedInitializedProductAsync(
            db,
            quantity: 100m,
            unitCost: 7000m);

        var service = new InventoryService(db);

        await service.ConsumeSaleStockAsync(
            product.Id,
            120m,
            SaleId,
            SaleNumber,
            UserId,
            CancellationToken.None);

        Assert.Equal(0m, product.StockQuantity);

        var originalMovement = await db.InventoryMovements
            .SingleAsync(movement =>
                movement.SaleId == SaleId &&
                movement.Type == InventoryMovementType.Sale);

        Assert.Equal(-100m, originalMovement.QuantityChange);

        await service.ReverseSaleStockAsync(
            SaleId,
            SaleNumber,
            UserId,
            CancellationToken.None);

        Assert.Equal(100m, product.StockQuantity);

        var reversal = await db.InventoryMovements
            .SingleAsync(movement =>
                movement.Type == InventoryMovementType.SaleReversal);

        Assert.Equal(100m, reversal.QuantityChange);
    }

    [Fact]
    public async Task ReverseSaleStock_WhenWarehouseWasEmpty_DoesNothing()
    {
        await using var db = CreateDb();
        var product = await SeedInitializedProductAsync(
            db,
            quantity: 0m,
            unitCost: 7000m);

        var service = new InventoryService(db);

        await service.ConsumeSaleStockAsync(
            product.Id,
            20m,
            SaleId,
            SaleNumber,
            UserId,
            CancellationToken.None);

        Assert.False(
            await db.InventoryMovements.AnyAsync(
                movement =>
                    movement.SaleId == SaleId &&
                    movement.Type == InventoryMovementType.Sale));

        await service.ReverseSaleStockAsync(
            SaleId,
            SaleNumber,
            UserId,
            CancellationToken.None);

        Assert.Equal(0m, product.StockQuantity);

        Assert.False(
            await db.InventoryMovements.AnyAsync(
                movement =>
                    movement.Type == InventoryMovementType.SaleReversal));
    }

    [Fact]
    public async Task ReverseSaleStock_RestoresExactOriginalFifoLayers()
    {
        await using var db = CreateDb();
        var product = await SeedInitializedProductAsync(
            db,
            quantity: 30m,
            unitCost: 1000m);

        var firstLayer = await db.InventoryCostLayers
            .SingleAsync(layer => layer.ProductId == product.Id);

        var secondSource = new InventoryMovement
        {
            ProductId = product.Id,
            Type = InventoryMovementType.AdjustmentIncrease,
            QuantityChange = 20m,
            StockBefore = 30m,
            StockAfter = 50m,
            UnitCost = null,
            AdjustmentReason = InventoryAdjustmentReason.RegistrationError,
            Notes = "Second FIFO layer fixture.",
            CreatedByUserId = UserId,
            CreatedAtUtc = DateTime.UtcNow.AddSeconds(1)
        };

        db.InventoryMovements.Add(secondSource);
        await db.SaveChangesAsync();

        var secondCreatedAtUtc = secondSource.CreatedAtUtc;

        var secondLayer = new InventoryCostLayer
        {
            ProductId = product.Id,
            SourceMovementId = secondSource.Id,
            OriginalQuantity = 20m,
            RemainingQuantity = 20m,
            UnitCost = 1500m,
            CreatedAtUtc = secondCreatedAtUtc
        };

        db.InventoryCostLayers.Add(secondLayer);
        product.StockQuantity = 50m;
        await db.SaveChangesAsync();

        var service = new InventoryService(db);

        await service.ConsumeSaleStockAsync(
            product.Id,
            40m,
            SaleId,
            SaleNumber,
            UserId,
            CancellationToken.None);

        Assert.Equal(0m, firstLayer.RemainingQuantity);
        Assert.Equal(10m, secondLayer.RemainingQuantity);

        var saleMovement = await db.InventoryMovements
            .SingleAsync(movement =>
                movement.SaleId == SaleId &&
                movement.Type == InventoryMovementType.Sale);

        var originalConsumptions = await db.InventoryCostConsumptions
            .Where(item =>
                item.InventoryMovementId == saleMovement.Id)
            .OrderBy(item => item.InventoryCostLayerId)
            .ToListAsync();

        Assert.Equal(2, originalConsumptions.Count);
        Assert.Contains(
            originalConsumptions,
            item =>
                item.InventoryCostLayerId == firstLayer.Id &&
                item.Quantity == 30m &&
                item.UnitCostSnapshot == 1000m);
        Assert.Contains(
            originalConsumptions,
            item =>
                item.InventoryCostLayerId == secondLayer.Id &&
                item.Quantity == 10m &&
                item.UnitCostSnapshot == 1500m);

        await service.ReverseSaleStockAsync(
            SaleId,
            SaleNumber,
            UserId,
            CancellationToken.None);

        Assert.Equal(50m, product.StockQuantity);
        Assert.Equal(30m, firstLayer.RemainingQuantity);
        Assert.Equal(20m, secondLayer.RemainingQuantity);

        Assert.Equal(
            2,
            await db.InventoryCostLayers.CountAsync(
                layer => layer.ProductId == product.Id));

        var persistedConsumptions = await db.InventoryCostConsumptions
            .Where(item =>
                item.InventoryMovementId == saleMovement.Id)
            .OrderBy(item => item.InventoryCostLayerId)
            .ToListAsync();

        Assert.Equal(2, persistedConsumptions.Count);
        Assert.Equal(
            originalConsumptions.Select(item => item.Id),
            persistedConsumptions.Select(item => item.Id));
    }

    [Fact]
    public async Task ReverseSaleStock_WhenCalledTwice_IsIdempotent()
    {
        await using var db = CreateDb();
        var product = await SeedInitializedProductAsync(
            db,
            quantity: 100m,
            unitCost: 7000m);

        var service = new InventoryService(db);

        await service.ConsumeSaleStockAsync(
            product.Id,
            80m,
            SaleId,
            SaleNumber,
            UserId,
            CancellationToken.None);

        await service.ReverseSaleStockAsync(
            SaleId,
            SaleNumber,
            UserId,
            CancellationToken.None);

        await service.ReverseSaleStockAsync(
            SaleId,
            SaleNumber,
            UserId,
            CancellationToken.None);

        Assert.Equal(100m, product.StockQuantity);

        Assert.Equal(
            1,
            await db.InventoryMovements.CountAsync(
                movement =>
                    movement.Type ==
                    InventoryMovementType.SaleReversal));
    }

    [Fact]
    public async Task ReverseSaleStock_WhenRestorationWouldExceedOriginalLayer_ThrowsWithoutRestoring()
    {
        await using var db = CreateDb();
        var product = await SeedInitializedProductAsync(
            db,
            quantity: 100m,
            unitCost: 7000m);

        var service = new InventoryService(db);

        await service.ConsumeSaleStockAsync(
            product.Id,
            80m,
            SaleId,
            SaleNumber,
            UserId,
            CancellationToken.None);

        var layer = await db.InventoryCostLayers
            .SingleAsync(item => item.ProductId == product.Id);

        layer.RemainingQuantity = 30m;
        product.StockQuantity = 30m;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InventoryCostIntegrityException>(
            () => service.ReverseSaleStockAsync(
                SaleId,
                SaleNumber,
                UserId,
                CancellationToken.None));

        Assert.Equal(30m, product.StockQuantity);
        Assert.Equal(30m, layer.RemainingQuantity);

        Assert.False(
            await db.InventoryMovements.AnyAsync(
                movement =>
                    movement.Type ==
                    InventoryMovementType.SaleReversal));
    }

    [Fact]
    public async Task ReverseSaleStock_WithMultipleProducts_RestoresEachProduct()
    {
        await using var db = CreateDb();

        var firstProduct = await SeedInitializedProductAsync(
            db,
            quantity: 20m,
            unitCost: 1000m,
            suffix: "A");

        var secondProduct = await SeedInitializedProductAsync(
            db,
            quantity: 15m,
            unitCost: 2000m,
            suffix: "B");

        var service = new InventoryService(db);

        await service.ConsumeSaleStockAsync(
            firstProduct.Id,
            12m,
            SaleId,
            SaleNumber,
            UserId,
            CancellationToken.None);

        await service.ConsumeSaleStockAsync(
            secondProduct.Id,
            10m,
            SaleId,
            SaleNumber,
            UserId,
            CancellationToken.None);

        Assert.Equal(8m, firstProduct.StockQuantity);
        Assert.Equal(5m, secondProduct.StockQuantity);

        await service.ReverseSaleStockAsync(
            SaleId,
            SaleNumber,
            UserId,
            CancellationToken.None);

        Assert.Equal(20m, firstProduct.StockQuantity);
        Assert.Equal(15m, secondProduct.StockQuantity);

        Assert.Equal(
            2,
            await db.InventoryMovements.CountAsync(
                movement =>
                    movement.Type ==
                    InventoryMovementType.SaleReversal));
    }

    [Fact]
    public async Task ReverseSaleStock_WhenOnlySomeMovementsWereAlreadyReversed_Throws()
    {
        await using var db = CreateDb();

        var firstProduct = await SeedInitializedProductAsync(
            db,
            quantity: 20m,
            unitCost: 1000m,
            suffix: "A");

        var secondProduct = await SeedInitializedProductAsync(
            db,
            quantity: 15m,
            unitCost: 2000m,
            suffix: "B");

        var service = new InventoryService(db);

        await service.ConsumeSaleStockAsync(
            firstProduct.Id,
            12m,
            SaleId,
            SaleNumber,
            UserId,
            CancellationToken.None);

        await service.ConsumeSaleStockAsync(
            secondProduct.Id,
            10m,
            SaleId,
            SaleNumber,
            UserId,
            CancellationToken.None);

        var firstSaleMovement = await db.InventoryMovements
            .OrderBy(movement => movement.Id)
            .FirstAsync(movement =>
                movement.SaleId == SaleId &&
                movement.Type == InventoryMovementType.Sale);

        db.InventoryMovements.Add(
            new InventoryMovement
            {
                ProductId = firstSaleMovement.ProductId,
                SaleId = SaleId,
                ReversesInventoryMovementId =
                    firstSaleMovement.Id,
                Type = InventoryMovementType.SaleReversal,
                QuantityChange =
                    Math.Abs(firstSaleMovement.QuantityChange),
                StockBefore = 8m,
                StockAfter = 20m,
                UnitCost = null,
                AdjustmentReason = null,
                Notes = "Partial reversal fixture.",
                CreatedByUserId = UserId,
                CreatedAtUtc = DateTime.UtcNow
            });

        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InventoryCostIntegrityException>(
            () => service.ReverseSaleStockAsync(
                SaleId,
                SaleNumber,
                UserId,
                CancellationToken.None));
    }

    private static RevestikDbContext CreateDb() =>
        new(
            new DbContextOptionsBuilder<RevestikDbContext>()
                .UseInMemoryDatabase(
                    $"inventory-sale-reversal-{Guid.NewGuid()}")
                .Options);

    private static async Task<Product> SeedInitializedProductAsync(
        RevestikDbContext db,
        decimal quantity,
        decimal unitCost,
        string suffix = "")
    {
        await EnsureUserAsync(db);

        var category = new ProductCategory
        {
            Name = $"Categoría {Guid.NewGuid():N}",
            CreatedAtUtc = DateTime.UtcNow
        };

        var unit = new UnitOfMeasure
        {
            Name = $"Caja {Guid.NewGuid():N}",
            Symbol = $"c{Guid.NewGuid():N}"[..10],
            CreatedAtUtc = DateTime.UtcNow
        };

        db.AddRange(category, unit);
        await db.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            Name = $"Producto reversión {suffix} {Guid.NewGuid():N}",
            Description = "Fixture de reversión FIFO.",
            CabysCode = "1234567890123",
            InventoryUnitId = unit.Id,
            CommercialUnitId = unit.Id,
            CommercialUnitsPerInventoryUnit = 1m,
            RequiresWholeInventoryUnits = true,
            SalePrice = 10000m,
            CurrentCost = unitCost,
            TaxRate = 13m,
            StockQuantity = 0m,
            MinimumStock = 0m,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        var service = new InventoryService(db);

        await service.RegisterInitialStockAsync(
            product.Id,
            new InitialStockRequest
            {
                Quantity = quantity,
                Notes = "Inventario inicial para reversión."
            },
            UserId,
            CancellationToken.None);

        return product;
    }

    private static async Task EnsureUserAsync(
        RevestikDbContext db)
    {
        if (await db.Users.AnyAsync(user => user.Id == UserId))
            return;

        db.Users.Add(
            new ApplicationUser
            {
                Id = UserId,
                UserName = "inventory-reversal@example.com",
                NormalizedUserName =
                    "INVENTORY-REVERSAL@EXAMPLE.COM",
                Email = "inventory-reversal@example.com",
                NormalizedEmail =
                    "INVENTORY-REVERSAL@EXAMPLE.COM",
                EmailConfirmed = true,
                DisplayName = "Inventory Reversal User",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });

        await db.SaveChangesAsync();
    }
}