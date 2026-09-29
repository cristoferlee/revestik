using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Tests.Inventory;

public sealed class InventorySaleReversalDataIntegrityIntegrationTests(
    SqlServerIntegrationTestFixture sqlServerFixture)
    : IClassFixture<SqlServerIntegrationTestFixture>, IAsyncLifetime
{
    private const string UserId =
        "inventory-sale-reversal-sql-user";

    private int productId;
    private int originalMovementId;

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

        var existingUser = await db.Users
            .SingleOrDefaultAsync(user => user.Id == UserId);

        if (existingUser is null)
        {
            db.Users.Add(
                new ApplicationUser
                {
                    Id = UserId,
                    UserName =
                        "inventory-sale-reversal-sql@example.com",
                    NormalizedUserName =
                        "INVENTORY-SALE-REVERSAL-SQL@EXAMPLE.COM",
                    Email =
                        "inventory-sale-reversal-sql@example.com",
                    NormalizedEmail =
                        "INVENTORY-SALE-REVERSAL-SQL@EXAMPLE.COM",
                    EmailConfirmed = true,
                    DisplayName =
                        "Inventory Sale Reversal SQL User",
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
            Name = $"Reversal SQL {Guid.NewGuid():N}",
            CreatedAtUtc = DateTime.UtcNow
        };

        var unit = new UnitOfMeasure
        {
            Name = $"Caja SQL {Guid.NewGuid():N}",
            Symbol = $"s{Guid.NewGuid():N}"[..10],
            CreatedAtUtc = DateTime.UtcNow
        };

        db.AddRange(category, unit);
        await db.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            Name = $"Producto SQL {Guid.NewGuid():N}",
            Description = "SQL reversal fixture.",
            CabysCode = "1234567890123",
            InventoryUnitId = unit.Id,
            CommercialUnitId = unit.Id,
            CommercialUnitsPerInventoryUnit = 1m,
            RequiresWholeInventoryUnits = true,
            SalePrice = 10000m,
            CurrentCost = 7000m,
            TaxRate = 13m,
            StockQuantity = 20m,
            MinimumStock = 0m,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        var original = new InventoryMovement
        {
            ProductId = product.Id,
            Type = InventoryMovementType.Sale,
            QuantityChange = -10m,
            StockBefore = 30m,
            StockAfter = 20m,
            UnitCost = null,
            AdjustmentReason = null,
            Notes = "Venta VEN-SQL-001. Salida de bodega: 10.",
            CreatedByUserId = UserId,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.InventoryMovements.Add(original);
        await db.SaveChangesAsync();

        productId = product.Id;
        originalMovementId = original.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SecondReversal_ForSameSaleMovement_IsRejectedBySqlServer()
    {
        await using (var firstContext =
            sqlServerFixture.CreateDbContext())
        {
            firstContext.InventoryMovements.Add(
                CreateReversalMovement());

            await firstContext.SaveChangesAsync();
        }

        await using var secondContext =
            sqlServerFixture.CreateDbContext();

        secondContext.InventoryMovements.Add(
            CreateReversalMovement());

        await Assert.ThrowsAsync<DbUpdateException>(
            () => secondContext.SaveChangesAsync());
    }

    private InventoryMovement CreateReversalMovement() =>
        new()
        {
            ProductId = productId,
            ReversesInventoryMovementId = originalMovementId,
            Type = InventoryMovementType.SaleReversal,
            QuantityChange = 10m,
            StockBefore = 20m,
            StockAfter = 30m,
            UnitCost = null,
            AdjustmentReason = null,
            Notes = "Anulación SQL fixture.",
            CreatedByUserId = UserId,
            CreatedAtUtc = DateTime.UtcNow
        };
}