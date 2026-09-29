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
}