using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Inventory;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Tests.Inventory;

public sealed class InventorySaleConsumptionTests
{
    private const string UserId =
        "inventory-sale-consumption-user";

    private const int SaleId = 900001;

    [Fact]
    public async Task ConsumeSaleStock_WithEnoughStock_ConsumesFifoAndUpdatesStock()
    {
        await using var dbContext = CreateDbContext();

        var product = await SeedProductAsync(
            dbContext,
            stock: 100m,
            conversion: 1m,
            requiresWholeUnits: true);

        var service = new InventoryService(dbContext);

        await service.ConsumeSaleStockAsync(
            product.Id,
            80m,
            SaleId,
            "VEN-TEST-001",
            UserId,
            CancellationToken.None);

        Assert.Equal(20m, product.StockQuantity);

        var movement = await dbContext.InventoryMovements
            .SingleAsync(item =>
                item.ProductId == product.Id &&
                item.Type == InventoryMovementType.Sale);

        Assert.Equal(SaleId, movement.SaleId);
        Assert.Equal(-80m, movement.QuantityChange);
        Assert.Equal(100m, movement.StockBefore);
        Assert.Equal(20m, movement.StockAfter);

        var layer = await dbContext.InventoryCostLayers
            .SingleAsync(item =>
                item.ProductId == product.Id);

        Assert.Equal(20m, layer.RemainingQuantity);

        var consumption = await dbContext.InventoryCostConsumptions
            .SingleAsync(item =>
                item.InventoryMovementId == movement.Id);

        Assert.Equal(80m, consumption.Quantity);
    }

    [Fact]
    public async Task ConsumeSaleStock_AboveAvailableStock_StopsAtZero()
    {
        await using var dbContext = CreateDbContext();

        var product = await SeedProductAsync(
            dbContext,
            stock: 100m,
            conversion: 1m,
            requiresWholeUnits: true);

        var service = new InventoryService(dbContext);

        await service.ConsumeSaleStockAsync(
            product.Id,
            120m,
            SaleId,
            "VEN-TEST-002",
            UserId,
            CancellationToken.None);

        Assert.Equal(0m, product.StockQuantity);

        var movement = await dbContext.InventoryMovements
            .SingleAsync(item =>
                item.ProductId == product.Id &&
                item.Type == InventoryMovementType.Sale);

        Assert.Equal(SaleId, movement.SaleId);
        Assert.Equal(-100m, movement.QuantityChange);
        Assert.Equal(0m, movement.StockAfter);
        Assert.Contains(
            "Faltante directo/especial: 20",
            movement.Notes);
    }

    [Fact]
    public async Task ConsumeSaleStock_UsesCommercialToPhysicalConversion()
    {
        await using var dbContext = CreateDbContext();

        var product = await SeedProductAsync(
            dbContext,
            stock: 10m,
            conversion: 1.44m,
            requiresWholeUnits: true);

        var service = new InventoryService(dbContext);

        await service.ConsumeSaleStockAsync(
            product.Id,
            11.52m,
            SaleId,
            "VEN-TEST-003",
            UserId,
            CancellationToken.None);

        Assert.Equal(2m, product.StockQuantity);

        var movement = await dbContext.InventoryMovements
            .SingleAsync(item =>
                item.ProductId == product.Id &&
                item.Type == InventoryMovementType.Sale);

        Assert.Equal(SaleId, movement.SaleId);
        Assert.Equal(-8m, movement.QuantityChange);
    }

    [Fact]
    public async Task ConsumeSaleStock_WhenWarehouseIsEmpty_DoesNotCreateFakeMovement()
    {
        await using var dbContext = CreateDbContext();

        var product = await SeedProductAsync(
            dbContext,
            stock: 0m,
            conversion: 1m,
            requiresWholeUnits: true);

        var service = new InventoryService(dbContext);

        await service.ConsumeSaleStockAsync(
            product.Id,
            20m,
            SaleId,
            "VEN-TEST-004",
            UserId,
            CancellationToken.None);

        Assert.Equal(0m, product.StockQuantity);

        var saleMovements = await dbContext.InventoryMovements
            .CountAsync(item =>
                item.ProductId == product.Id &&
                item.Type == InventoryMovementType.Sale);

        Assert.Equal(0, saleMovements);
    }

    private static RevestikDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase(
                $"InventorySaleConsumptionTests-{Guid.NewGuid()}")
            .Options;

        return new RevestikDbContext(options);
    }

    private static async Task<Product> SeedProductAsync(
        RevestikDbContext dbContext,
        decimal stock,
        decimal conversion,
        bool requiresWholeUnits)
    {
        var category = new ProductCategory
        {
            Name = "Pegamentos",
            CreatedAtUtc = DateTime.UtcNow
        };

        var physicalUnit = new UnitOfMeasure
        {
            Name = "Saco",
            Symbol = "saco",
            CreatedAtUtc = DateTime.UtcNow
        };

        var commercialUnit = new UnitOfMeasure
        {
            Name = "Unidad comercial",
            Symbol = "und",
            CreatedAtUtc = DateTime.UtcNow
        };

        var user = new ApplicationUser
        {
            Id = UserId,
            UserName = "inventory-sale@example.com",
            NormalizedUserName = "INVENTORY-SALE@EXAMPLE.COM",
            Email = "inventory-sale@example.com",
            NormalizedEmail = "INVENTORY-SALE@EXAMPLE.COM",
            EmailConfirmed = true,
            DisplayName = "Inventory Sale User",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.AddRange(
            category,
            physicalUnit,
            commercialUnit,
            user);

        await dbContext.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            Name = "Pegamento Test",
            Description = "Producto para prueba de salida por venta.",
            CabysCode = "1234567890123",
            InventoryUnitId = physicalUnit.Id,
            CommercialUnitId = commercialUnit.Id,
            CommercialUnitsPerInventoryUnit = conversion,
            RequiresWholeInventoryUnits = requiresWholeUnits,
            SalePrice = 10000m,
            CurrentCost = 7000m,
            TaxRate = 13m,
            StockQuantity = 0m,
            MinimumStock = 5m,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var service = new InventoryService(dbContext);

        await service.RegisterInitialStockAsync(
            product.Id,
            new InitialStockRequest
            {
                Quantity = stock,
                Notes = "Inventario inicial de prueba."
            },
            UserId,
            CancellationToken.None);

        return product;
    }
}