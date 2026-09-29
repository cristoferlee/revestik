using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Inventory;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Tests.Inventory;

public sealed class InventoryServiceInitialStockTests
{
    private const string UserId = "inventory-service-test-user";

    [Fact]
    public async Task RegisterInitialStock_WithPositiveQuantity_UpdatesStockAndCreatesMovement()
    {
        await using var dbContext = CreateDbContext();
        var product = await SeedAsync(dbContext);
        var service = new InventoryService(dbContext);

        var result = await service.RegisterInitialStockAsync(
            product.Id,
            new InitialStockRequest
            {
                Quantity = 5m,
                Notes = "  Apertura de inventario.  "
            },
            UserId,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(5m, result.QuantityChange);
        Assert.Equal(0m, result.StockBefore);
        Assert.Equal(5m, result.StockAfter);
        Assert.Equal(product.CurrentCost, result.UnitCost);
        Assert.Equal("Apertura de inventario.", result.Notes);
        Assert.Equal(UserId, result.CreatedByUserId);

        var reloadedProduct = await dbContext.Products
            .SingleAsync(item => item.Id == product.Id);

        Assert.Equal(5m, reloadedProduct.StockQuantity);

        var movement = await dbContext.InventoryMovements.SingleAsync();
        Assert.Equal(InventoryMovementType.InitialStock, movement.Type);
        Assert.Equal(5m, movement.StockAfter);
    }

    [Fact]
    public async Task RegisterInitialStock_WithZeroQuantity_CreatesZeroMovement()
    {
        await using var dbContext = CreateDbContext();
        var product = await SeedAsync(dbContext);
        var service = new InventoryService(dbContext);

        var result = await service.RegisterInitialStockAsync(
            product.Id,
            new InitialStockRequest { Quantity = 0m },
            UserId,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0m, result.QuantityChange);
        Assert.Equal(0m, result.StockAfter);
        Assert.Single(await dbContext.InventoryMovements.ToListAsync());
    }

    [Fact]
    public async Task RegisterInitialStock_Twice_ThrowsConflict()
    {
        await using var dbContext = CreateDbContext();
        var product = await SeedAsync(dbContext);
        var service = new InventoryService(dbContext);

        await service.RegisterInitialStockAsync(
            product.Id,
            new InitialStockRequest { Quantity = 0m },
            UserId,
            CancellationToken.None);

        await Assert.ThrowsAsync<InitialStockAlreadyRegisteredException>(
            () => service.RegisterInitialStockAsync(
                product.Id,
                new InitialStockRequest { Quantity = 1m },
                UserId,
                CancellationToken.None));
    }

    [Fact]
    public async Task RegisterInitialStock_WithFractionForWholeUnitProduct_Throws()
    {
        await using var dbContext = CreateDbContext();
        var product = await SeedAsync(
            dbContext,
            requiresWholeInventoryUnits: true);
        var service = new InventoryService(dbContext);

        await Assert.ThrowsAsync<InvalidInventoryOperationException>(
            () => service.RegisterInitialStockAsync(
                product.Id,
                new InitialStockRequest { Quantity = 1.5m },
                UserId,
                CancellationToken.None));

        Assert.Equal(0m, product.StockQuantity);
        Assert.Empty(await dbContext.InventoryMovements.ToListAsync());
    }

    [Fact]
    public async Task RegisterInitialStock_ForDeletedProduct_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var product = await SeedAsync(dbContext);
        product.IsDeleted = true;
        await dbContext.SaveChangesAsync();

        var service = new InventoryService(dbContext);

        var result = await service.RegisterInitialStockAsync(
            product.Id,
            new InitialStockRequest { Quantity = 1m },
            UserId,
            CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(await dbContext.InventoryMovements.ToListAsync());
    }

    [Fact]
    public async Task RegisterInitialStock_WithInactiveUser_Throws()
    {
        await using var dbContext = CreateDbContext();
        var product = await SeedAsync(dbContext);
        var user = await dbContext.Users.SingleAsync(user => user.Id == UserId);
        user.IsActive = false;
        await dbContext.SaveChangesAsync();

        var service = new InventoryService(dbContext);

        await Assert.ThrowsAsync<InvalidInventoryUserException>(
            () => service.RegisterInitialStockAsync(
                product.Id,
                new InitialStockRequest { Quantity = 1m },
                UserId,
                CancellationToken.None));

        Assert.Equal(0m, product.StockQuantity);
        Assert.Empty(await dbContext.InventoryMovements.ToListAsync());
    }

    private static RevestikDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase(
                $"InventoryServiceTests-{Guid.NewGuid()}")
            .Options;

        return new RevestikDbContext(options);
    }

    private static async Task<Product> SeedAsync(
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
            UserName = "inventory-service-test@example.com",
            NormalizedUserName = "INVENTORY-SERVICE-TEST@EXAMPLE.COM",
            Email = "inventory-service-test@example.com",
            NormalizedEmail = "INVENTORY-SERVICE-TEST@EXAMPLE.COM",
            DisplayName = "Inventory Test User",
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
            RequiresWholeInventoryUnits = requiresWholeInventoryUnits,
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