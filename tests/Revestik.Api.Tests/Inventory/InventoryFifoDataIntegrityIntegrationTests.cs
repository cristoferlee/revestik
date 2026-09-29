using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Inventory;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Tests.Inventory;

public sealed class InventoryFifoDataIntegrityIntegrationTests(
    SqlServerIntegrationTestFixture sqlServerFixture)
    : IClassFixture<SqlServerIntegrationTestFixture>, IAsyncLifetime
{
    private const string UserId = "fifo-sql-user";

    private int productId;

    public async Task InitializeAsync()
    {
        await using var db = sqlServerFixture.CreateDbContext();

        await db.InventoryCostConsumptions.ExecuteDeleteAsync();
        await db.InventoryCostLayers.ExecuteDeleteAsync();
        await db.InventoryMovements.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
        await db.ProductCategories.ExecuteDeleteAsync();
        await db.UnitsOfMeasure.ExecuteDeleteAsync();

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == UserId);

        if (user is null)
        {
            db.Users.Add(new ApplicationUser
            {
                Id = UserId,
                UserName = "fifo-sql@example.com",
                NormalizedUserName = "FIFO-SQL@EXAMPLE.COM",
                Email = "fifo-sql@example.com",
                NormalizedEmail = "FIFO-SQL@EXAMPLE.COM",
                EmailConfirmed = true,
                DisplayName = "FIFO SQL User",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            user.IsActive = true;
        }

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

        db.AddRange(category, unit);
        await db.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            Name = "Pegamento Premium",
            Description = "Saco 25 kg",
            CabysCode = "1234567890123",
            InventoryUnitId = unit.Id,
            CommercialUnitId = unit.Id,
            CommercialUnitsPerInventoryUnit = 1m,
            RequiresWholeInventoryUnits = true,
            SalePrice = 2500m,
            CurrentCost = 1000m,
            TaxRate = 13m,
            StockQuantity = 0m,
            MinimumStock = 5m,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        productId = product.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task InitialStock_CreatesPersistedKnownCostLayer()
    {
        await using var db = sqlServerFixture.CreateDbContext();
        var service = new InventoryService(db);

        await service.RegisterInitialStockAsync(
            productId,
            new InitialStockRequest
            {
                Quantity = 30m,
                Notes = "Inventario inicial."
            },
            UserId,
            CancellationToken.None);

        await using var verify = sqlServerFixture.CreateDbContext();

        var product = await verify.Products
            .AsNoTracking()
            .SingleAsync(x => x.Id == productId);

        var layer = await verify.InventoryCostLayers
            .AsNoTracking()
            .SingleAsync(x => x.ProductId == productId);

        Assert.Equal(30m, product.StockQuantity);
        Assert.Equal(30m, layer.OriginalQuantity);
        Assert.Equal(30m, layer.RemainingQuantity);
        Assert.Equal(1000m, layer.UnitCost);
    }

    [Fact]
    public async Task AdjustmentIncrease_CreatesUnknownCostLayer()
    {
        await SeedInitialStockAsync(30m);

        await using var db = sqlServerFixture.CreateDbContext();
        var service = new InventoryService(db);

        await service.AdjustStockAsync(
            productId,
            new InventoryAdjustmentRequest
            {
                NewStockQuantity = 35m,
                Reason = InventoryAdjustmentReason.PhysicalCount,
                Notes = "Cinco sacos adicionales."
            },
            UserId,
            CancellationToken.None);

        await using var verify = sqlServerFixture.CreateDbContext();

        var layers = await verify.InventoryCostLayers
            .AsNoTracking()
            .Where(x => x.ProductId == productId)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .ToListAsync();

        Assert.Equal(2, layers.Count);
        Assert.Equal(1000m, layers[0].UnitCost);
        Assert.Null(layers[1].UnitCost);
        Assert.Equal(5m, layers[1].RemainingQuantity);
    }

    [Fact]
    public async Task AdjustmentDecrease_ConsumesOldestCostFirst()
    {
        await SeedInitialStockAsync(30m);

        await using (var db = sqlServerFixture.CreateDbContext())
        {
            var product = await db.Products
                .SingleAsync(x => x.Id == productId);

            var now = DateTime.UtcNow.AddSeconds(1);

            var movement = new InventoryMovement
            {
                ProductId = productId,
                Type = InventoryMovementType.AdjustmentIncrease,
                QuantityChange = 30m,
                StockBefore = 30m,
                StockAfter = 60m,
                UnitCost = null,
                AdjustmentReason = InventoryAdjustmentReason.RegistrationError,
                Notes = "Fixture segunda capa.",
                CreatedByUserId = UserId,
                CreatedAtUtc = now
            };

            db.InventoryMovements.Add(movement);
            await db.SaveChangesAsync();

            db.InventoryCostLayers.Add(new InventoryCostLayer
            {
                ProductId = productId,
                SourceMovementId = movement.Id,
                OriginalQuantity = 30m,
                RemainingQuantity = 30m,
                UnitCost = 1500m,
                CreatedAtUtc = now
            });

            product.StockQuantity = 60m;
            await db.SaveChangesAsync();
        }

        await using (var db = sqlServerFixture.CreateDbContext())
        {
            var service = new InventoryService(db);

            await service.AdjustStockAsync(
                productId,
                new InventoryAdjustmentRequest
                {
                    NewStockQuantity = 0m,
                    Reason = InventoryAdjustmentReason.RegistrationError,
                    Notes = "Consumo FIFO completo."
                },
                UserId,
                CancellationToken.None);
        }

        await using var verify = sqlServerFixture.CreateDbContext();

        var consumptions = await verify.InventoryCostConsumptions
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync();

        Assert.Equal(2, consumptions.Count);
        Assert.Equal(30m, consumptions[0].Quantity);
        Assert.Equal(1000m, consumptions[0].UnitCostSnapshot);
        Assert.Equal(30m, consumptions[1].Quantity);
        Assert.Equal(1500m, consumptions[1].UnitCostSnapshot);

        Assert.Equal(
            0m,
            await verify.InventoryCostLayers
                .Where(x => x.ProductId == productId)
                .SumAsync(x => x.RemainingQuantity));
    }

    [Fact]
    public async Task CostLayer_WithInvalidRemainingQuantity_IsRejectedBySqlServer()
    {
        await SeedInitialStockAsync(10m);

        await using var db = sqlServerFixture.CreateDbContext();

        var source = new InventoryMovement
        {
            ProductId = productId,
            Type = InventoryMovementType.AdjustmentIncrease,
            QuantityChange = 1m,
            StockBefore = 10m,
            StockAfter = 11m,
            UnitCost = null,
            AdjustmentReason = InventoryAdjustmentReason.RegistrationError,
            Notes = "Fixture constraint.",
            CreatedByUserId = UserId,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.InventoryMovements.Add(source);
        await db.SaveChangesAsync();

        db.InventoryCostLayers.Add(new InventoryCostLayer
        {
            ProductId = productId,
            SourceMovementId = source.Id,
            OriginalQuantity = 1m,
            RemainingQuantity = 2m,
            UnitCost = 1000m,
            CreatedAtUtc = source.CreatedAtUtc
        });

        await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync());
    }

    [Fact]
    public async Task FailedFifoSave_RollsBackStockLayersAndConsumption()
    {
        await SeedInitialStockAsync(10m);

        await using (var db = sqlServerFixture.CreateDbContext())
        {
            var product = await db.Products
                .SingleAsync(x => x.Id == productId);

            var layer = await db.InventoryCostLayers
                .SingleAsync(x => x.ProductId == productId);

            var movement = new InventoryMovement
            {
                ProductId = productId,
                Type = InventoryMovementType.AdjustmentDecrease,
                QuantityChange = -5m,
                StockBefore = 10m,
                StockAfter = 5m,
                UnitCost = null,
                AdjustmentReason = InventoryAdjustmentReason.Damage,
                Notes = "Rollback test.",
                CreatedByUserId = UserId,
                CreatedAtUtc = DateTime.UtcNow
            };

            product.StockQuantity = 5m;
            layer.RemainingQuantity = 5m;
            db.InventoryMovements.Add(movement);

            db.InventoryCostConsumptions.Add(new InventoryCostConsumption
            {
                InventoryMovement = movement,
                InventoryCostLayer = layer,
                Quantity = -5m,
                UnitCostSnapshot = layer.UnitCost,
                CreatedAtUtc = DateTime.UtcNow
            });

            await Assert.ThrowsAsync<DbUpdateException>(
                () => db.SaveChangesAsync());
        }

        await using var verify = sqlServerFixture.CreateDbContext();

        var productAfter = await verify.Products
            .AsNoTracking()
            .SingleAsync(x => x.Id == productId);

        var layerAfter = await verify.InventoryCostLayers
            .AsNoTracking()
            .SingleAsync(x => x.ProductId == productId);

        Assert.Equal(10m, productAfter.StockQuantity);
        Assert.Equal(10m, layerAfter.RemainingQuantity);
        Assert.Empty(
            await verify.InventoryCostConsumptions
                .AsNoTracking()
                .ToListAsync());
    }

    private async Task SeedInitialStockAsync(decimal quantity)
    {
        await using var db = sqlServerFixture.CreateDbContext();
        var service = new InventoryService(db);

        await service.RegisterInitialStockAsync(
            productId,
            new InitialStockRequest
            {
                Quantity = quantity,
                Notes = "Inventario inicial."
            },
            UserId,
            CancellationToken.None);
    }
}