using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Inventory;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Tests.Inventory;

public sealed class InventoryServiceAdjustmentTests
{
    private const string UserId =
        "inventory-adjustment-test-user";

    [Fact]
    public async Task AdjustStock_Increase_CalculatesMovementFromCurrentStock()
    {
        await using var dbContext = CreateDbContext();
        var product = await SeedInitializedProductAsync(
            dbContext,
            currentStock: 5m);

        var service = new InventoryService(dbContext);

        var result = await service.AdjustStockAsync(
            product.Id,
            new InventoryAdjustmentRequest
            {
                NewStockQuantity = 8m,
                Reason = InventoryAdjustmentReason.PhysicalCount,
                Notes = " Conteo físico. "
            },
            UserId,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(
            InventoryMovementType.AdjustmentIncrease,
            result.Type);
        Assert.Equal(5m, result.StockBefore);
        Assert.Equal(3m, result.QuantityChange);
        Assert.Equal(8m, result.StockAfter);
        Assert.Null(result.UnitCost);
        Assert.Equal(
            InventoryAdjustmentReason.PhysicalCount,
            result.AdjustmentReason);
        Assert.Equal("Conteo físico.", result.Notes);

        Assert.Equal(8m, product.StockQuantity);
    }

    [Fact]
    public async Task AdjustStock_Decrease_CalculatesMovementFromCurrentStock()
    {
        await using var dbContext = CreateDbContext();
        var product = await SeedInitializedProductAsync(
            dbContext,
            currentStock: 10m);

        var service = new InventoryService(dbContext);

        var result = await service.AdjustStockAsync(
            product.Id,
            new InventoryAdjustmentRequest
            {
                NewStockQuantity = 7m,
                Reason = InventoryAdjustmentReason.Damage,
                Notes = "Producto dañado."
            },
            UserId,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(
            InventoryMovementType.AdjustmentDecrease,
            result.Type);
        Assert.Equal(-3m, result.QuantityChange);
        Assert.Equal(7m, product.StockQuantity);
    }

    [Fact]
    public async Task AdjustStock_WithSameStock_Throws()
    {
        await using var dbContext = CreateDbContext();
        var product = await SeedInitializedProductAsync(
            dbContext,
            currentStock: 5m);

        var service = new InventoryService(dbContext);

        await Assert.ThrowsAsync<InvalidInventoryOperationException>(
            () => service.AdjustStockAsync(
                product.Id,
                new InventoryAdjustmentRequest
                {
                    NewStockQuantity = 5m,
                    Reason = InventoryAdjustmentReason.PhysicalCount,
                    Notes = "Sin diferencia."
                },
                UserId,
                CancellationToken.None));
    }

    [Fact]
    public async Task AdjustStock_WithoutInitialStock_Throws()
    {
        await using var dbContext = CreateDbContext();
        var product = await SeedProductAsync(dbContext);
        var service = new InventoryService(dbContext);

        await Assert.ThrowsAsync<InvalidInventoryOperationException>(
            () => service.AdjustStockAsync(
                product.Id,
                new InventoryAdjustmentRequest
                {
                    NewStockQuantity = 2m,
                    Reason = InventoryAdjustmentReason.RegistrationError,
                    Notes = "Corrección."
                },
                UserId,
                CancellationToken.None));
    }

    [Fact]
    public async Task AdjustStock_FractionForWholeUnitProduct_Throws()
    {
        await using var dbContext = CreateDbContext();
        var product = await SeedInitializedProductAsync(
            dbContext,
            currentStock: 5m,
            requiresWholeInventoryUnits: true);

        var service = new InventoryService(dbContext);

        await Assert.ThrowsAsync<InvalidInventoryOperationException>(
            () => service.AdjustStockAsync(
                product.Id,
                new InventoryAdjustmentRequest
                {
                    NewStockQuantity = 4.5m,
                    Reason = InventoryAdjustmentReason.PhysicalCount,
                    Notes = "Conteo."
                },
                UserId,
                CancellationToken.None));
    }

    private static RevestikDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase(
                $"InventoryAdjustmentTests-{Guid.NewGuid()}")
            .Options;

        return new RevestikDbContext(options);
    }

    private static async Task<Product> SeedInitializedProductAsync(
        RevestikDbContext dbContext,
        decimal currentStock,
        bool requiresWholeInventoryUnits = false)
    {
        var product = await SeedProductAsync(
            dbContext,
            requiresWholeInventoryUnits);

        var service = new InventoryService(dbContext);

        await service.RegisterInitialStockAsync(
            product.Id,
            new InitialStockRequest
            {
                Quantity = currentStock,
                Notes = "Inventario inicial."
            },
            UserId,
            CancellationToken.None);

        return product;
    }

    private static async Task<Product> SeedProductAsync(
        RevestikDbContext dbContext,
        bool requiresWholeInventoryUnits = false)
    {
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

        var user = new ApplicationUser
        {
            Id = UserId,
            UserName = "inventory-adjustment@example.com",
            NormalizedUserName = "INVENTORY-ADJUSTMENT@EXAMPLE.COM",
            Email = "inventory-adjustment@example.com",
            NormalizedEmail = "INVENTORY-ADJUSTMENT@EXAMPLE.COM",
            DisplayName = "Inventory Adjustment User",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.AddRange(category, unit, user);
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
            RequiresWholeInventoryUnits =
                requiresWholeInventoryUnits,
            SalePrice = 15000m,
            CurrentCost = 10000m,
            TaxRate = 13m,
            StockQuantity = 0m,
            MinimumStock = 5m,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        return product;
    }
}