using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Inventory;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Tests.Inventory;

public sealed class InventoryServiceFifoIntegrationTests
{
    private const string UserId = "fifo-service-user";

    [Fact]
    public async Task InitialStock_CreatesKnownCostLayer()
    {
        await using var db = CreateDb();
        var product = await SeedProductAsync(db, 1000m);
        var service = new InventoryService(db);

        await service.RegisterInitialStockAsync(
            product.Id,
            new InitialStockRequest { Quantity = 30m, Notes = "Inicial." },
            UserId,
            CancellationToken.None);

        var layer = Assert.Single(db.InventoryCostLayers.Local);
        Assert.Equal(30m, layer.OriginalQuantity);
        Assert.Equal(30m, layer.RemainingQuantity);
        Assert.Equal(1000m, layer.UnitCost);
    }

    [Fact]
    public async Task InitialStock_Zero_CreatesNoLayer()
    {
        await using var db = CreateDb();
        var product = await SeedProductAsync(db, 1000m);
        var service = new InventoryService(db);

        await service.RegisterInitialStockAsync(
            product.Id,
            new InitialStockRequest { Quantity = 0m, Notes = "Inicial cero." },
            UserId,
            CancellationToken.None);

        Assert.Empty(db.InventoryCostLayers.Local);
        Assert.Single(db.InventoryMovements.Local);
    }

    [Fact]
    public async Task AdjustmentIncrease_CreatesUnknownCostLayer()
    {
        await using var db = CreateDb();
        var product = await SeedInitializedAsync(db, 30m, 1000m);
        var service = new InventoryService(db);

        await service.AdjustStockAsync(
            product.Id,
            new InventoryAdjustmentRequest
            {
                NewStockQuantity = 35m,
                Reason = InventoryAdjustmentReason.PhysicalCount,
                Notes = "Conteo físico."
            },
            UserId,
            CancellationToken.None);

        var layers = db.InventoryCostLayers.Local.ToList();
        Assert.Equal(2, layers.Count);
        Assert.Contains(layers, x => x.UnitCost == 1000m && x.RemainingQuantity == 30m);
        Assert.Contains(layers, x => x.UnitCost == null && x.RemainingQuantity == 5m);
    }

    [Fact]
    public async Task AdjustmentDecrease_ConsumesHistoricalCostsInFifoOrder()
    {
        await using var db = CreateDb();
        var product = await SeedInitializedAsync(db, 30m, 1000m);

        var secondMovement = new InventoryMovement
        {
            ProductId = product.Id,
            Type = InventoryMovementType.AdjustmentIncrease,
            QuantityChange = 30m,
            StockBefore = 30m,
            StockAfter = 60m,
            UnitCost = null,
            AdjustmentReason = InventoryAdjustmentReason.RegistrationError,
            Notes = "Second cost layer fixture.",
            CreatedByUserId = UserId,
            CreatedAtUtc = DateTime.UtcNow.AddSeconds(1)
        };
        db.InventoryMovements.Add(secondMovement);
        await db.SaveChangesAsync();

        db.InventoryCostLayers.Add(new InventoryCostLayer
        {
            ProductId = product.Id,
            SourceMovementId = secondMovement.Id,
            OriginalQuantity = 30m,
            RemainingQuantity = 30m,
            UnitCost = 1500m,
            CreatedAtUtc = secondMovement.CreatedAtUtc
        });
        product.StockQuantity = 60m;
        await db.SaveChangesAsync();

        var service = new InventoryService(db);

        await service.AdjustStockAsync(
            product.Id,
            new InventoryAdjustmentRequest
            {
                NewStockQuantity = 0m,
                Reason = InventoryAdjustmentReason.RegistrationError,
                Notes = "FIFO test."
            },
            UserId,
            CancellationToken.None);

        var consumptions = db.InventoryCostConsumptions.Local.ToList();
        Assert.Equal(2, consumptions.Count);
        Assert.Contains(consumptions, x => x.Quantity == 30m && x.UnitCostSnapshot == 1000m);
        Assert.Contains(consumptions, x => x.Quantity == 30m && x.UnitCostSnapshot == 1500m);
    }

    [Fact]
    public async Task Adjustment_WhenLayersDoNotMatchStock_Throws()
    {
        await using var db = CreateDb();
        var product = await SeedInitializedAsync(db, 10m, 1000m);
        product.StockQuantity = 12m;
        await db.SaveChangesAsync();

        var service = new InventoryService(db);

        await Assert.ThrowsAsync<InventoryCostIntegrityException>(() =>
            service.AdjustStockAsync(
                product.Id,
                new InventoryAdjustmentRequest
                {
                    NewStockQuantity = 11m,
                    Reason = InventoryAdjustmentReason.PhysicalCount,
                    Notes = "Desbalance."
                },
                UserId,
                CancellationToken.None));
    }

    private static RevestikDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"fifo-wire-{Guid.NewGuid()}")
            .Options);

    private static async Task<Product> SeedProductAsync(
        RevestikDbContext db,
        decimal cost)
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
            CreatedAtUtc = DateTime.UtcNow
        };
        var user = new ApplicationUser
        {
            Id = UserId,
            UserName = "fifo@example.com",
            NormalizedUserName = "FIFO@EXAMPLE.COM",
            Email = "fifo@example.com",
            NormalizedEmail = "FIFO@EXAMPLE.COM",
            DisplayName = "FIFO User",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.AddRange(category, unit, user);
        await db.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            Name = "Pegamento",
            Description = "Saco",
            CabysCode = "1234567890123",
            InventoryUnitId = unit.Id,
            CommercialUnitId = unit.Id,
            CommercialUnitsPerInventoryUnit = 1m,
            RequiresWholeInventoryUnits = true,
            SalePrice = 2000m,
            CurrentCost = cost,
            TaxRate = 13m,
            StockQuantity = 0m,
            MinimumStock = 0m,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    private static async Task<Product> SeedInitializedAsync(
        RevestikDbContext db,
        decimal quantity,
        decimal cost)
    {
        var product = await SeedProductAsync(db, cost);
        var service = new InventoryService(db);
        await service.RegisterInitialStockAsync(
            product.Id,
            new InitialStockRequest { Quantity = quantity, Notes = "Inicial." },
            UserId,
            CancellationToken.None);
        return product;
    }
}