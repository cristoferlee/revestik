using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Tests.Inventory;

public sealed class InventoryMovementDataIntegrityIntegrationTests(
    SqlServerIntegrationTestFixture sqlServerFixture)
    : IClassFixture<SqlServerIntegrationTestFixture>, IAsyncLifetime
{
    private const string UserId =
        "inventory-sql-integration-user";

    private int productId;

    public async Task InitializeAsync()
    {
        await using var dbContext =
            sqlServerFixture.CreateDbContext();

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
                        "inventory-sql-integration@example.com",
                    NormalizedUserName =
                        "INVENTORY-SQL-INTEGRATION@EXAMPLE.COM",
                    Email =
                        "inventory-sql-integration@example.com",
                    NormalizedEmail =
                        "INVENTORY-SQL-INTEGRATION@EXAMPLE.COM",
                    EmailConfirmed = true,
                    DisplayName = "Inventory SQL Integration User",
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
        }
        else
        {
            existingUser.IsActive = true;
            existingUser.DisplayName =
                "Inventory SQL Integration User";
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
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task InitialStock_WithInvalidBalance_IsRejectedBySqlServer()
    {
        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        dbContext.InventoryMovements.Add(
            CreateInitialStockMovement(
                quantityChange: 5m,
                stockBefore: 0m,
                stockAfter: 4m));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task InitialStock_WithNegativeQuantity_IsRejectedBySqlServer()
    {
        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        dbContext.InventoryMovements.Add(
            CreateInitialStockMovement(
                quantityChange: -1m,
                stockBefore: 0m,
                stockAfter: -1m));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task InitialStock_WithoutPositiveUnitCost_IsRejectedBySqlServer()
    {
        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        var movement = CreateInitialStockMovement(
            quantityChange: 0m,
            stockBefore: 0m,
            stockAfter: 0m);

        movement.UnitCost = 0m;

        dbContext.InventoryMovements.Add(movement);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task SecondInitialStock_ForSameProduct_IsRejectedBySqlServer()
    {
        await using (var firstContext =
            sqlServerFixture.CreateDbContext())
        {
            firstContext.InventoryMovements.Add(
                CreateInitialStockMovement(
                    quantityChange: 0m,
                    stockBefore: 0m,
                    stockAfter: 0m));

            await firstContext.SaveChangesAsync();
        }

        await using var secondContext =
            sqlServerFixture.CreateDbContext();

        secondContext.InventoryMovements.Add(
            CreateInitialStockMovement(
                quantityChange: 1m,
                stockBefore: 0m,
                stockAfter: 1m));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => secondContext.SaveChangesAsync());
    }

    [Fact]
    public async Task FailedMovementSave_RollsBackProductStockUpdate()
    {
        await using (var dbContext =
            sqlServerFixture.CreateDbContext())
        {
            var product = await dbContext.Products
                .SingleAsync(item => item.Id == productId);

            product.StockQuantity = 5m;

            dbContext.InventoryMovements.Add(
                CreateInitialStockMovement(
                    quantityChange: 5m,
                    stockBefore: 0m,
                    stockAfter: 4m));

            await Assert.ThrowsAsync<DbUpdateException>(
                () => dbContext.SaveChangesAsync());
        }

        await using var verificationContext =
            sqlServerFixture.CreateDbContext();

        var persistedProduct =
            await verificationContext.Products
                .AsNoTracking()
                .SingleAsync(item => item.Id == productId);

        Assert.Equal(0m, persistedProduct.StockQuantity);

        Assert.False(
            await verificationContext.InventoryMovements
                .AsNoTracking()
                .AnyAsync());
    }

    private InventoryMovement CreateInitialStockMovement(
        decimal quantityChange,
        decimal stockBefore,
        decimal stockAfter)
    {
        return new InventoryMovement
        {
            ProductId = productId,
            Type = InventoryMovementType.InitialStock,
            QuantityChange = quantityChange,
            StockBefore = stockBefore,
            StockAfter = stockAfter,
            UnitCost = 10000m,
            Notes = string.Empty,
            CreatedByUserId = UserId,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}