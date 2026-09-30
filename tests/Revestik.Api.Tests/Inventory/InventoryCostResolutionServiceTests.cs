using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Inventory;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Tests.Inventory;

public sealed class InventoryCostResolutionServiceTests
{
    private const string UserId = "cost-resolution-user";

    [Fact]
    public async Task ResolveAsync_MovesLayerOutOfUnknownCostSummary()
    {
        await using var dbContext = CreateDbContext();

        var unit = new UnitOfMeasure
        {
            Name = "Unidad",
            Symbol = "U",
            RequiresWholeQuantity = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var category = new ProductCategory
        {
            Name = "Cost Resolution",
            CreatedAtUtc = DateTime.UtcNow
        };

        var user = new ApplicationUser
        {
            Id = UserId,
            UserName = "cost-resolution@example.com",
            NormalizedUserName = "COST-RESOLUTION@EXAMPLE.COM",
            Email = "cost-resolution@example.com",
            NormalizedEmail = "COST-RESOLUTION@EXAMPLE.COM",
            EmailConfirmed = true,
            DisplayName = "Cost Resolution User",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.AddRange(unit, category, user);
        await dbContext.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            Name = "Producto pendiente",
            Description = string.Empty,
            CabysCode = "1234567890123",
            InventoryUnitId = unit.Id,
            CommercialUnitId = unit.Id,
            CommercialUnitsPerInventoryUnit = 1m,
            RequiresWholeInventoryUnits = true,
            SalePrice = 1000m,
            CurrentCost = 500m,
            TaxRate = 13m,
            StockQuantity = 18m,
            MinimumStock = 0m,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var movement = new InventoryMovement
        {
            ProductId = product.Id,
            Type = InventoryMovementType.AdjustmentIncrease,
            QuantityChange = 18m,
            StockBefore = 0m,
            StockAfter = 18m,
            AdjustmentReason = InventoryAdjustmentReason.PhysicalCount,
            Notes = "Conteo físico.",
            CreatedByUserId = UserId,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.InventoryMovements.Add(movement);
        await dbContext.SaveChangesAsync();

        var layer = new InventoryCostLayer
        {
            ProductId = product.Id,
            SourceMovementId = movement.Id,
            OriginalQuantity = 18m,
            RemainingQuantity = 18m,
            UnitCost = null,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.InventoryCostLayers.Add(layer);
        await dbContext.SaveChangesAsync();

        var inventoryService = new InventoryService(dbContext);
        var resolutionService =
            new InventoryCostResolutionService(dbContext);

        var before =
            await inventoryService.GetSummaryAsync(
                CancellationToken.None);

        Assert.Equal(18m, before.UnknownCostQuantity);
        Assert.Equal(1, before.ProductsWithUnknownCost);

        var resolved =
            await resolutionService.ResolveAsync(
                layer.Id,
                new ResolveInventoryCostRequest
                {
                    UnitCost = 2100m
                },
                UserId,
                CancellationToken.None);

        Assert.NotNull(resolved);
        Assert.Equal(2100m, resolved.ResolvedUnitCost);

        var after =
            await inventoryService.GetSummaryAsync(
                CancellationToken.None);

        Assert.Equal(0m, after.UnknownCostQuantity);
        Assert.Equal(0, after.ProductsWithUnknownCost);
        Assert.Equal(18m * 2100m, after.TotalInventoryCostValue);

        var persisted =
            await dbContext.InventoryCostLayers
                .AsNoTracking()
                .SingleAsync(item => item.Id == layer.Id);

        Assert.Null(persisted.UnitCost);
        Assert.Equal(2100m, persisted.ResolvedUnitCost);
        Assert.Equal(UserId, persisted.CostResolvedByUserId);
        Assert.NotNull(persisted.CostResolvedAtUtc);
    }

    private static RevestikDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<RevestikDbContext>()
                .UseInMemoryDatabase(
                    $"InventoryCostResolution-{Guid.NewGuid()}")
                .Options;

        return new RevestikDbContext(options);
    }
}
