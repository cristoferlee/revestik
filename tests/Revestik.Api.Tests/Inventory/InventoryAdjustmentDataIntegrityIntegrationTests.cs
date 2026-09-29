using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Inventory;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Tests.Inventory;

public sealed class InventoryAdjustmentDataIntegrityIntegrationTests(
    SqlServerIntegrationTestFixture sqlServerFixture)
    : IClassFixture<SqlServerIntegrationTestFixture>, IAsyncLifetime
{
    private const string UserId =
        "inventory-adjustment-sql-user";

    private int productId;

    public async Task InitializeAsync()
    {
        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        await dbContext.InventoryCostConsumptions.ExecuteDeleteAsync();
        await dbContext.InventoryCostLayers.ExecuteDeleteAsync();
        await dbContext.InventoryMovements.ExecuteDeleteAsync();
        await dbContext.Products.ExecuteDeleteAsync();
        await dbContext.ProductCategories.ExecuteDeleteAsync();
        await dbContext.UnitsOfMeasure.ExecuteDeleteAsync();

        var existingUser =
            await dbContext.Users.SingleOrDefaultAsync(
                user => user.Id == UserId);

        if (existingUser is null)
        {
            dbContext.Users.Add(
                new ApplicationUser
                {
                    Id = UserId,
                    UserName =
                        "inventory-adjustment-sql@example.com",
                    NormalizedUserName =
                        "INVENTORY-ADJUSTMENT-SQL@EXAMPLE.COM",
                    Email =
                        "inventory-adjustment-sql@example.com",
                    NormalizedEmail =
                        "INVENTORY-ADJUSTMENT-SQL@EXAMPLE.COM",
                    EmailConfirmed = true,
                    DisplayName = "Inventory Adjustment SQL User",
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
        }
        else
        {
            existingUser.IsActive = true;
            existingUser.DisplayName =
                "Inventory Adjustment SQL User";
        }

        var category = new ProductCategory
        {
            Name = "Porcelanatos",
            CreatedAtUtc = DateTime.UtcNow
        };

        var unit = new UnitOfMeasure
        {
            Name = "Caja",
            Symbol = "caja",
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.AddRange(category, unit);
        await dbContext.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            Name = "Carrara White",
            Description = "60x120",
            CabysCode = "1234567890123",
            InventoryUnitId = unit.Id,
            CommercialUnitId = unit.Id,
            CommercialUnitsPerInventoryUnit = 1m,
            RequiresWholeInventoryUnits = true,
            SalePrice = 15000m,
            CurrentCost = 10000m,
            TaxRate = 13m,
            StockQuantity = 0m,
            MinimumStock = 5m,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        productId = product.Id;

        var service = new InventoryService(dbContext);

        await service.RegisterInitialStockAsync(
            productId,
            new InitialStockRequest
            {
                Quantity = 10m,
                Notes = "Inventario inicial."
            },
            UserId,
            CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AdjustmentIncrease_WithoutReason_IsRejectedBySqlServer()
    {
        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        dbContext.InventoryMovements.Add(
            new InventoryMovement
            {
                ProductId = productId,
                Type = InventoryMovementType.AdjustmentIncrease,
                QuantityChange = 1m,
                StockBefore = 10m,
                StockAfter = 11m,
                UnitCost = null,
                AdjustmentReason = null,
                Notes = "Ajuste.",
                CreatedByUserId = UserId,
                CreatedAtUtc = DateTime.UtcNow
            });

        await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task AdjustmentIncrease_WithNegativeChange_IsRejectedBySqlServer()
    {
        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        dbContext.InventoryMovements.Add(
            new InventoryMovement
            {
                ProductId = productId,
                Type = InventoryMovementType.AdjustmentIncrease,
                QuantityChange = -1m,
                StockBefore = 10m,
                StockAfter = 9m,
                UnitCost = null,
                AdjustmentReason =
                    InventoryAdjustmentReason.RegistrationError,
                Notes = "Ajuste.",
                CreatedByUserId = UserId,
                CreatedAtUtc = DateTime.UtcNow
            });

        await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task AdjustmentDecrease_WithPositiveChange_IsRejectedBySqlServer()
    {
        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        dbContext.InventoryMovements.Add(
            new InventoryMovement
            {
                ProductId = productId,
                Type = InventoryMovementType.AdjustmentDecrease,
                QuantityChange = 1m,
                StockBefore = 10m,
                StockAfter = 11m,
                UnitCost = null,
                AdjustmentReason =
                    InventoryAdjustmentReason.RegistrationError,
                Notes = "Ajuste.",
                CreatedByUserId = UserId,
                CreatedAtUtc = DateTime.UtcNow
            });

        await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task AdjustStock_PersistsReasonAndUpdatesBalanceAtomically()
    {
        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        var service = new InventoryService(dbContext);

        var result = await service.AdjustStockAsync(
            productId,
            new InventoryAdjustmentRequest
            {
                NewStockQuantity = 7m,
                Reason = InventoryAdjustmentReason.Damage,
                Notes = "Tres cajas dañadas."
            },
            UserId,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(
            InventoryMovementType.AdjustmentDecrease,
            result.Type);
        Assert.Equal(10m, result.StockBefore);
        Assert.Equal(-3m, result.QuantityChange);
        Assert.Equal(7m, result.StockAfter);
        Assert.Null(result.UnitCost);
        Assert.Equal(
            InventoryAdjustmentReason.Damage,
            result.AdjustmentReason);

        await using var verificationContext =
            sqlServerFixture.CreateDbContext();

        var product =
            await verificationContext.Products
                .AsNoTracking()
                .SingleAsync(item => item.Id == productId);

        var movement =
            await verificationContext.InventoryMovements
                .AsNoTracking()
                .SingleAsync(item =>
                    item.ProductId == productId &&
                    item.Type ==
                    InventoryMovementType.AdjustmentDecrease);

        Assert.Equal(7m, product.StockQuantity);
        Assert.Equal(10m, movement.StockBefore);
        Assert.Equal(-3m, movement.QuantityChange);
        Assert.Equal(7m, movement.StockAfter);
        Assert.Equal(
            InventoryAdjustmentReason.Damage,
            movement.AdjustmentReason);
        Assert.Equal(UserId, movement.CreatedByUserId);
    }

    [Fact]
    public async Task ProductRowVersion_ChangesWhenStockChanges()
    {
        byte[] rowVersionBefore;

        await using (var firstContext =
            sqlServerFixture.CreateDbContext())
        {
            rowVersionBefore =
                (await firstContext.Products
                    .AsNoTracking()
                    .SingleAsync(item => item.Id == productId))
                .RowVersion;
        }

        await using (var updateContext =
            sqlServerFixture.CreateDbContext())
        {
            var service = new InventoryService(updateContext);

            await service.AdjustStockAsync(
                productId,
                new InventoryAdjustmentRequest
                {
                    NewStockQuantity = 9m,
                    Reason =
                        InventoryAdjustmentReason.PhysicalCount,
                    Notes = "Conteo físico."
                },
                UserId,
                CancellationToken.None);
        }

        await using var verificationContext =
            sqlServerFixture.CreateDbContext();

        var rowVersionAfter =
            (await verificationContext.Products
                .AsNoTracking()
                .SingleAsync(item => item.Id == productId))
            .RowVersion;

        Assert.NotEmpty(rowVersionBefore);
        Assert.NotEmpty(rowVersionAfter);
        Assert.False(
            rowVersionBefore.SequenceEqual(rowVersionAfter));
    }

    [Fact]
    public async Task ConcurrentAdjustment_WithStaleProductVersion_ReturnsConflict()
    {
        await using var firstContext =
            sqlServerFixture.CreateDbContext();

        await using var secondContext =
            sqlServerFixture.CreateDbContext();

        _ = await firstContext.Products
            .SingleAsync(item => item.Id == productId);

        _ = await secondContext.Products
            .SingleAsync(item => item.Id == productId);

        var firstService =
            new InventoryService(firstContext);

        var secondService =
            new InventoryService(secondContext);

        var firstResult =
            await firstService.AdjustStockAsync(
                productId,
                new InventoryAdjustmentRequest
                {
                    NewStockQuantity = 9m,
                    Reason =
                        InventoryAdjustmentReason.PhysicalCount,
                    Notes = "Primer conteo."
                },
                UserId,
                CancellationToken.None);

        Assert.NotNull(firstResult);

        await Assert.ThrowsAsync<InventoryConcurrencyException>(
            () => secondService.AdjustStockAsync(
                productId,
                new InventoryAdjustmentRequest
                {
                    NewStockQuantity = 8m,
                    Reason =
                        InventoryAdjustmentReason.PhysicalCount,
                    Notes = "Segundo conteo."
                },
                UserId,
                CancellationToken.None));

        await using var verificationContext =
            sqlServerFixture.CreateDbContext();

        var product =
            await verificationContext.Products
                .AsNoTracking()
                .SingleAsync(item => item.Id == productId);

        var adjustments =
            await verificationContext.InventoryMovements
                .AsNoTracking()
                .Where(item =>
                    item.ProductId == productId &&
                    item.Type !=
                    InventoryMovementType.InitialStock)
                .ToListAsync();

        Assert.Equal(9m, product.StockQuantity);
        Assert.Single(adjustments);
        Assert.Equal(9m, adjustments[0].StockAfter);
    }
}