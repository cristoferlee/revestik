using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.Inventory;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Tests.Inventory;

public sealed class InventorySummaryTests
{
    [Fact]
    public async Task GetSummary_ReturnsStockStatusCounts()
    {
        await using var db = CreateDbContext();

        var products = await SeedProductsAsync(db);

        var service = new InventoryService(db);

        var summary = await service.GetSummaryAsync(
            CancellationToken.None);

        Assert.Equal(3, summary.TotalProducts);
        Assert.Equal(1, summary.InStockCount);
        Assert.Equal(1, summary.LowStockCount);
        Assert.Equal(1, summary.OutOfStockCount);
    }

    [Fact]
    public async Task GetSummary_UsesHistoricalFifoLayerCosts()
    {
        await using var db = CreateDbContext();

        var products = await SeedProductsAsync(db);

        await AddLayerAsync(
            db,
            products[0],
            quantity: 5m,
            unitCost: 1000m,
            movementType: InventoryMovementType.InitialStock);

        await AddLayerAsync(
            db,
            products[0],
            quantity: 10m,
            unitCost: 1500m,
            movementType: InventoryMovementType.AdjustmentIncrease);

        var service = new InventoryService(db);

        var summary = await service.GetSummaryAsync(
            CancellationToken.None);

        Assert.Equal(20000m, summary.TotalInventoryCostValue);
    }

    [Fact]
    public async Task GetSummary_ReportsUnknownCostSeparately()
    {
        await using var db = CreateDbContext();

        var products = await SeedProductsAsync(db);

        await AddLayerAsync(
            db,
            products[0],
            quantity: 5m,
            unitCost: 1000m,
            movementType: InventoryMovementType.InitialStock);

        await AddLayerAsync(
            db,
            products[1],
            quantity: 3m,
            unitCost: null,
            movementType: InventoryMovementType.AdjustmentIncrease);

        var service = new InventoryService(db);

        var summary = await service.GetSummaryAsync(
            CancellationToken.None);

        Assert.Equal(5000m, summary.TotalInventoryCostValue);
        Assert.Equal(3m, summary.UnknownCostQuantity);
        Assert.Equal(1, summary.ProductsWithUnknownCost);
    }

    [Fact]
    public async Task GetSummary_ExcludesDeletedProductsAndTheirLayers()
    {
        await using var db = CreateDbContext();

        var products = await SeedProductsAsync(db);
        products[0].IsDeleted = true;
        await db.SaveChangesAsync();

        await AddLayerAsync(
            db,
            products[0],
            quantity: 5m,
            unitCost: 1000m,
            movementType: InventoryMovementType.InitialStock);

        var service = new InventoryService(db);

        var summary = await service.GetSummaryAsync(
            CancellationToken.None);

        Assert.Equal(2, summary.TotalProducts);
        Assert.Equal(0m, summary.TotalInventoryCostValue);
    }

    private static RevestikDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase(
                $"inventory-summary-{Guid.NewGuid()}")
            .Options;

        return new RevestikDbContext(options);
    }

    private static async Task<List<Product>> SeedProductsAsync(
        RevestikDbContext db)
    {
        var category = new ProductCategory
        {
            Name = "Inventory",
            CreatedAtUtc = DateTime.UtcNow
        };

        var unit = new UnitOfMeasure
        {
            Name = "Unidad",
            Symbol = "und",
            CreatedAtUtc = DateTime.UtcNow
        };

        db.AddRange(category, unit);
        await db.SaveChangesAsync();

        var products = new List<Product>
        {
            CreateProduct(
                category.Id,
                unit.Id,
                "In Stock",
                stock: 15m,
                minimum: 5m),
            CreateProduct(
                category.Id,
                unit.Id,
                "Low Stock",
                stock: 3m,
                minimum: 5m),
            CreateProduct(
                category.Id,
                unit.Id,
                "Out",
                stock: 0m,
                minimum: 5m)
        };

        db.Products.AddRange(products);
        await db.SaveChangesAsync();

        return products;
    }

    private static Product CreateProduct(
        int categoryId,
        int unitId,
        string name,
        decimal stock,
        decimal minimum)
    {
        return new Product
        {
            CategoryId = categoryId,
            Name = name,
            Description = name,
            CabysCode = "1234567890123",
            InventoryUnitId = unitId,
            CommercialUnitId = unitId,
            CommercialUnitsPerInventoryUnit = 1m,
            RequiresWholeInventoryUnits = true,
            SalePrice = 2000m,
            CurrentCost = 1000m,
            TaxRate = 13m,
            StockQuantity = stock,
            MinimumStock = minimum,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static async Task AddLayerAsync(
        RevestikDbContext db,
        Product product,
        decimal quantity,
        decimal? unitCost,
        InventoryMovementType movementType)
    {
        var now = DateTime.UtcNow;

        var movement = new InventoryMovement
        {
            ProductId = product.Id,
            Type = movementType,
            QuantityChange = quantity,
            StockBefore = 0m,
            StockAfter = quantity,
            UnitCost = movementType == InventoryMovementType.InitialStock
                ? unitCost
                : null,
            AdjustmentReason =
                movementType == InventoryMovementType.AdjustmentIncrease
                    ? Revestik.Shared.Inventory.InventoryAdjustmentReason.PhysicalCount
                    : null,
            Notes = "Summary test.",
            CreatedByUserId = "summary-test-user",
            CreatedAtUtc = now
        };

        db.InventoryMovements.Add(movement);
        await db.SaveChangesAsync();

        db.InventoryCostLayers.Add(
            new InventoryCostLayer
            {
                ProductId = product.Id,
                SourceMovementId = movement.Id,
                OriginalQuantity = quantity,
                RemainingQuantity = quantity,
                UnitCost = unitCost,
                CreatedAtUtc = now
            });

        await db.SaveChangesAsync();
    }
}
