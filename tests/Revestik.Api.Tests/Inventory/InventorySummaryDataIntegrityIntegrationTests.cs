using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Revestik.Api.Data;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Inventory;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Tests.Inventory;

public sealed class InventorySummaryDataIntegrityIntegrationTests(
    SqlServerIntegrationTestFixture sqlServerFixture)
    : IClassFixture<SqlServerIntegrationTestFixture>, IAsyncLifetime
{
    private const string UserId = "summary-sql-user";

    private int productId;

    public async Task InitializeAsync()
    {
        await using var db =
            sqlServerFixture.CreateDbContext();

        await db.InventoryCostConsumptions.ExecuteDeleteAsync();
        await db.InventoryCostLayers.ExecuteDeleteAsync();
        await db.InventoryMovements.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
        await db.ProductCategories.ExecuteDeleteAsync();
        await db.UnitsOfMeasure.ExecuteDeleteAsync();

        var existingUser =
            await db.Users.SingleOrDefaultAsync(
                user => user.Id == UserId);

        if (existingUser is null)
        {
            db.Users.Add(
                new ApplicationUser
                {
                    Id = UserId,
                    UserName = "summary-sql@example.com",
                    NormalizedUserName = "SUMMARY-SQL@EXAMPLE.COM",
                    Email = "summary-sql@example.com",
                    NormalizedEmail = "SUMMARY-SQL@EXAMPLE.COM",
                    EmailConfirmed = true,
                    DisplayName = "Inventory Summary SQL User",
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
        }
        else
        {
            existingUser.IsActive = true;
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
            Name = "Pegamento",
            Description = "Saco 25 kg",
            CabysCode = "1234567890123",
            InventoryUnitId = unit.Id,
            CommercialUnitId = unit.Id,
            CommercialUnitsPerInventoryUnit = 1m,
            RequiresWholeInventoryUnits = true,
            SalePrice = 2500m,
            CurrentCost = 9999m,
            TaxRate = 13m,
            StockQuantity = 8m,
            MinimumStock = 2m,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        productId = product.Id;

        var firstMovement = new InventoryMovement
        {
            ProductId = productId,
            Type = InventoryMovementType.InitialStock,
            QuantityChange = 5m,
            StockBefore = 0m,
            StockAfter = 5m,
            UnitCost = 1000m,
            AdjustmentReason = null,
            Notes = "Known cost.",
            CreatedByUserId = UserId,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2)
        };

        var secondMovement = new InventoryMovement
        {
            ProductId = productId,
            Type = InventoryMovementType.AdjustmentIncrease,
            QuantityChange = 3m,
            StockBefore = 5m,
            StockAfter = 8m,
            UnitCost = null,
            AdjustmentReason =
                InventoryAdjustmentReason.PhysicalCount,
            Notes = "Unknown cost.",
            CreatedByUserId = UserId,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
        };

        db.InventoryMovements.AddRange(
            firstMovement,
            secondMovement);

        await db.SaveChangesAsync();

        db.InventoryCostLayers.AddRange(
            new InventoryCostLayer
            {
                ProductId = productId,
                SourceMovementId = firstMovement.Id,
                OriginalQuantity = 5m,
                RemainingQuantity = 5m,
                UnitCost = 1000m,
                CreatedAtUtc = firstMovement.CreatedAtUtc
            },
            new InventoryCostLayer
            {
                ProductId = productId,
                SourceMovementId = secondMovement.Id,
                OriginalQuantity = 3m,
                RemainingQuantity = 3m,
                UnitCost = null,
                CreatedAtUtc = secondMovement.CreatedAtUtc
            });

        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetSummary_OnSqlServer_UsesFifoLayersNotCurrentCost()
    {
        await using var db =
            sqlServerFixture.CreateDbContext();

        var service = new InventoryService(db);

        var summary =
            await service.GetSummaryAsync(
                CancellationToken.None);

        Assert.Equal(1, summary.TotalProducts);
        Assert.Equal(1, summary.InStockCount);
        Assert.Equal(0, summary.LowStockCount);
        Assert.Equal(0, summary.OutOfStockCount);

        Assert.Equal(5000m, summary.TotalInventoryCostValue);
        Assert.Equal(3m, summary.UnknownCostQuantity);
        Assert.Equal(1, summary.ProductsWithUnknownCost);
    }

    [Fact]
    public async Task GetSummary_OnSqlServer_MatchesLegacySummaryWithTwoCommands()
    {
        var legacyCounter = new SqlCommandCounter();
        await using var legacyDb = CreateMeasuredContext(legacyCounter);
        var legacy = await ReadLegacySummaryAsync(legacyDb);
        Assert.Equal(7, legacyCounter.Count);

        var optimizedCounter = new SqlCommandCounter();
        await using var optimizedDb = CreateMeasuredContext(optimizedCounter);
        var optimized = await new InventoryService(optimizedDb)
            .GetSummaryAsync(CancellationToken.None);

        Assert.Equal(2, optimizedCounter.Count);
        Assert.Equal(legacy, optimized);
    }

    [Fact]
    public async Task GetSummary_OnSqlServer_ResolvesCostsAndCountsUnknownProductsOnce()
    {
        await using (var seed = sqlServerFixture.CreateDbContext())
        {
            var unresolvedLayer = await seed.InventoryCostLayers
                .SingleAsync(layer => !layer.UnitCost.HasValue);

            unresolvedLayer.ResolvedUnitCost = 700m;
            unresolvedLayer.CostResolvedByUserId = UserId;
            unresolvedLayer.CostResolvedAtUtc = DateTime.UtcNow;

            foreach (var quantity in new[] { 2m, 1m })
            {
                var movement = new InventoryMovement
                {
                    ProductId = productId,
                    Type = InventoryMovementType.AdjustmentIncrease,
                    QuantityChange = quantity,
                    StockBefore = 8m,
                    StockAfter = 8m + quantity,
                    UnitCost = null,
                    AdjustmentReason = InventoryAdjustmentReason.PhysicalCount,
                    Notes = "Additional unknown cost.",
                    CreatedByUserId = UserId,
                    CreatedAtUtc = DateTime.UtcNow
                };
                seed.InventoryMovements.Add(movement);
                await seed.SaveChangesAsync();

                seed.InventoryCostLayers.Add(new InventoryCostLayer
                {
                    ProductId = productId,
                    SourceMovementId = movement.Id,
                    OriginalQuantity = quantity,
                    RemainingQuantity = quantity,
                    UnitCost = null,
                    CreatedAtUtc = movement.CreatedAtUtc
                });
                await seed.SaveChangesAsync();
            }
        }

        var counter = new SqlCommandCounter();
        await using var db = CreateMeasuredContext(counter);
        var summary = await new InventoryService(db).GetSummaryAsync(
            CancellationToken.None);

        Assert.Equal(7100m, summary.TotalInventoryCostValue);
        Assert.Equal(3m, summary.UnknownCostQuantity);
        Assert.Equal(1, summary.ProductsWithUnknownCost);
        Assert.Equal(2, counter.Count);
    }

    [Fact]
    public async Task GetSummary_OnSqlServer_EmptyCatalogueReturnsZeros()
    {
        await using (var seed = sqlServerFixture.CreateDbContext())
        {
            await seed.InventoryCostLayers.ExecuteDeleteAsync();
            await seed.InventoryMovements.ExecuteDeleteAsync();
            await seed.Products.ExecuteDeleteAsync();
        }

        var counter = new SqlCommandCounter();
        await using var db = CreateMeasuredContext(counter);
        var summary = await new InventoryService(db).GetSummaryAsync(
            CancellationToken.None);

        Assert.Equal(new InventorySummaryResponse(0, 0, 0, 0, 0m, 0m, 0), summary);
        Assert.Equal(2, counter.Count);
    }

    [Fact]
    public async Task GetSummary_OnSqlServer_IgnoresExhaustedLayers()
    {
        await using (var seed = sqlServerFixture.CreateDbContext())
        {
            var layers = await seed.InventoryCostLayers.ToListAsync();
            foreach (var layer in layers)
                layer.RemainingQuantity = 0m;
            await seed.SaveChangesAsync();
        }

        var counter = new SqlCommandCounter();
        await using var db = CreateMeasuredContext(counter);
        var summary = await new InventoryService(db).GetSummaryAsync(
            CancellationToken.None);

        Assert.Equal(1, summary.TotalProducts);
        Assert.Equal(1, summary.InStockCount);
        Assert.Equal(0m, summary.TotalInventoryCostValue);
        Assert.Equal(0m, summary.UnknownCostQuantity);
        Assert.Equal(0, summary.ProductsWithUnknownCost);
        Assert.Equal(2, counter.Count);
    }

    [Fact]
    public async Task GetSummary_OnSqlServer_ExcludesDeletedProductsAndTheirLayers()
    {
        await using (var seed = sqlServerFixture.CreateDbContext())
        {
            var product = await seed.Products.SingleAsync(p => p.Id == productId);
            product.IsDeleted = true;
            await seed.SaveChangesAsync();
        }

        var counter = new SqlCommandCounter();
        await using var db = CreateMeasuredContext(counter);
        var summary = await new InventoryService(db).GetSummaryAsync(
            CancellationToken.None);

        Assert.Equal(new InventorySummaryResponse(0, 0, 0, 0, 0m, 0m, 0), summary);
        Assert.Equal(2, counter.Count);
    }

    private RevestikDbContext CreateMeasuredContext(SqlCommandCounter interceptor)
    {
        var options = new DbContextOptionsBuilder<RevestikDbContext>()
            .UseSqlServer(sqlServerFixture.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;
        return new RevestikDbContext(options);
    }

    // The seven-query implementation is retained as a test-only reference.
    private static async Task<InventorySummaryResponse> ReadLegacySummaryAsync(
        RevestikDbContext db, CancellationToken ct = default)
    {
        var products = db.Products.AsNoTracking()
            .Where(product => !product.IsDeleted);

        var total = await products.CountAsync(ct);
        var inStock = await products.CountAsync(
            product => product.StockQuantity > product.MinimumStock, ct);
        var lowStock = await products.CountAsync(
            product => product.StockQuantity > 0m &&
                       product.StockQuantity <= product.MinimumStock, ct);
        var outOfStock = await products.CountAsync(
            product => product.StockQuantity == 0m, ct);

        var layers = db.InventoryCostLayers.AsNoTracking()
            .Where(layer => !layer.Product.IsDeleted &&
                            layer.RemainingQuantity > 0m);

        var inventoryCost = await layers
            .Where(layer => layer.UnitCost.HasValue ||
                            layer.ResolvedUnitCost.HasValue)
            .SumAsync(layer => layer.RemainingQuantity *
                (layer.UnitCost ?? layer.ResolvedUnitCost)!.Value, ct);
        var unknownQuantity = await layers
            .Where(layer => !layer.UnitCost.HasValue &&
                            !layer.ResolvedUnitCost.HasValue)
            .SumAsync(layer => layer.RemainingQuantity, ct);
        var unknownProducts = await layers
            .Where(layer => !layer.UnitCost.HasValue &&
                            !layer.ResolvedUnitCost.HasValue)
            .Select(layer => layer.ProductId).Distinct().CountAsync(ct);

        return new InventorySummaryResponse(total, inStock, lowStock,
            outOfStock, inventoryCost, unknownQuantity, unknownProducts);
    }

    private sealed class SqlCommandCounter : DbCommandInterceptor
    {
        private int count;
        public int Count => Volatile.Read(ref count);

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref count);
            return new ValueTask<DbDataReader>(result);
        }

        public override ValueTask<object?> ScalarExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            object? result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref count);
            return new ValueTask<object?>(result);
        }
    }
}