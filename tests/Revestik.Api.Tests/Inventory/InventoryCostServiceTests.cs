using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.Inventory;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Tests.Inventory;

public sealed class InventoryCostServiceTests
{
    [Fact]
    public async Task ConsumeFifo_ConsumesOldestLayerFirst()
    {
        await using var db = CreateDb();
        var product = await SeedProductAsync(db, 13m);

        var m1 = Movement(product.Id, 5m, 8000m, DateTime.UtcNow.AddMinutes(-2));
        var m2 = Movement(product.Id, 8m, 9000m, DateTime.UtcNow.AddMinutes(-1));
        db.InventoryMovements.AddRange(m1, m2);
        await db.SaveChangesAsync();

        var l1 = Layer(product.Id, m1.Id, 5m, 8000m, m1.CreatedAtUtc);
        var l2 = Layer(product.Id, m2.Id, 8m, 9000m, m2.CreatedAtUtc);
        db.InventoryCostLayers.AddRange(l1, l2);
        await db.SaveChangesAsync();

        var outgoing = new InventoryMovement
        {
            ProductId = product.Id,
            Type = InventoryMovementType.AdjustmentDecrease,
            QuantityChange = -10m,
            StockBefore = 13m,
            StockAfter = 3m,
            AdjustmentReason = InventoryAdjustmentReason.Damage,
            Notes = "Daño.",
            CreatedByUserId = "user",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.InventoryMovements.Add(outgoing);

        var service = new InventoryCostService(db);
        await service.ConsumeFifoAsync(
            product, outgoing, 10m, outgoing.CreatedAtUtc, CancellationToken.None);

        Assert.Equal(0m, l1.RemainingQuantity);
        Assert.Equal(3m, l2.RemainingQuantity);

        var items = db.InventoryCostConsumptions.Local.ToList();
        Assert.Equal(2, items.Count);
        Assert.Contains(items, x => x.Quantity == 5m && x.UnitCostSnapshot == 8000m);
        Assert.Contains(items, x => x.Quantity == 5m && x.UnitCostSnapshot == 9000m);
    }

    [Fact]
    public async Task ConsumeFifo_AllowsUnknownCost()
    {
        await using var db = CreateDb();
        var product = await SeedProductAsync(db, 2m);
        var source = Movement(product.Id, 2m, null, DateTime.UtcNow);
        db.InventoryMovements.Add(source);
        await db.SaveChangesAsync();

        var layer = Layer(product.Id, source.Id, 2m, null, source.CreatedAtUtc);
        db.InventoryCostLayers.Add(layer);
        await db.SaveChangesAsync();

        var outgoing = new InventoryMovement
        {
            ProductId = product.Id,
            Type = InventoryMovementType.AdjustmentDecrease,
            QuantityChange = -1m,
            StockBefore = 2m,
            StockAfter = 1m,
            AdjustmentReason = InventoryAdjustmentReason.PhysicalCount,
            Notes = "Conteo.",
            CreatedByUserId = "user",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.InventoryMovements.Add(outgoing);

        var service = new InventoryCostService(db);
        await service.ConsumeFifoAsync(
            product, outgoing, 1m, outgoing.CreatedAtUtc, CancellationToken.None);

        var consumption = Assert.Single(db.InventoryCostConsumptions.Local);
        Assert.Null(consumption.UnitCostSnapshot);
        Assert.Equal(1m, layer.RemainingQuantity);
    }

    [Fact]
    public async Task EnsureBalance_WhenLayersDifferFromStock_Throws()
    {
        await using var db = CreateDb();
        var product = await SeedProductAsync(db, 5m);
        var source = Movement(product.Id, 4m, 1000m, DateTime.UtcNow);
        db.InventoryMovements.Add(source);
        await db.SaveChangesAsync();
        db.InventoryCostLayers.Add(
            Layer(product.Id, source.Id, 4m, 1000m, source.CreatedAtUtc));
        await db.SaveChangesAsync();

        var service = new InventoryCostService(db);

        await Assert.ThrowsAsync<InventoryCostIntegrityException>(
            () => service.EnsureBalanceAsync(
                product.Id, 5m, CancellationToken.None));
    }

    [Fact]
    public async Task CreateLayer_AllowsUnknownCost()
    {
        await using var db = CreateDb();
        var product = await SeedProductAsync(db, 0m);
        var source = Movement(product.Id, 3m, null, DateTime.UtcNow);
        db.InventoryMovements.Add(source);

        var service = new InventoryCostService(db);
        service.CreateLayer(product, source, 3m, null, source.CreatedAtUtc);

        var layer = Assert.Single(db.InventoryCostLayers.Local);
        Assert.Equal(3m, layer.OriginalQuantity);
        Assert.Equal(3m, layer.RemainingQuantity);
        Assert.Null(layer.UnitCost);
    }

    private static RevestikDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"fifo-{Guid.NewGuid()}")
            .Options);

    private static async Task<Product> SeedProductAsync(
        RevestikDbContext db, decimal stock)
    {
        var product = new Product
        {
            Name = "Pegamento",
            Description = "Saco",
            CabysCode = "1234567890123",
            CommercialUnitsPerInventoryUnit = 1m,
            RequiresWholeInventoryUnits = true,
            SalePrice = 2000m,
            CurrentCost = 1000m,
            TaxRate = 13m,
            StockQuantity = stock,
            MinimumStock = 0m,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    private static InventoryMovement Movement(
        int productId, decimal quantity, decimal? cost, DateTime createdAtUtc) =>
        new()
        {
            ProductId = productId,
            Type = InventoryMovementType.InitialStock,
            QuantityChange = quantity,
            StockBefore = 0m,
            StockAfter = quantity,
            UnitCost = cost,
            Notes = string.Empty,
            CreatedByUserId = "user",
            CreatedAtUtc = createdAtUtc
        };

    private static InventoryCostLayer Layer(
        int productId, int sourceMovementId, decimal quantity,
        decimal? cost, DateTime createdAtUtc) =>
        new()
        {
            ProductId = productId,
            SourceMovementId = sourceMovementId,
            OriginalQuantity = quantity,
            RemainingQuantity = quantity,
            UnitCost = cost,
            CreatedAtUtc = createdAtUtc
        };
}