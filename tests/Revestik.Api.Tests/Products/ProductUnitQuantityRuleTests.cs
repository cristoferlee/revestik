using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.Products;
using Revestik.Shared.Products;

namespace Revestik.Api.Tests.Products;

public sealed class ProductUnitQuantityRuleTests
{
    [Fact]
    public async Task CreateAsync_WholeInventoryUnit_ForcesWholeProductQuantity()
    {
        await using var dbContext = CreateDbContext();

        var category = new ProductCategory
        {
            Name = "Pegamentos",
            CreatedAtUtc = DateTime.UtcNow
        };

        var unit = new UnitOfMeasure
        {
            Name = "Unidad",
            Symbol = "U",
            RequiresWholeQuantity = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.AddRange(category, unit);
        await dbContext.SaveChangesAsync();

        var service = new ProductService(dbContext);

        var created = await service.CreateAsync(
            new ProductUpsertRequest
            {
                CategoryId = category.Id,
                Name = "Bondex",
                Description = "20 kg",
                CabysCode = "3751000000000",
                InventoryUnitId = unit.Id,
                CommercialUnitId = unit.Id,
                CommercialUnitsPerInventoryUnit = 1m,
                RequiresWholeInventoryUnits = false,
                SalePrice = 5500m,
                CurrentCost = 2100m,
                TaxRate = 13m,
                MinimumStock = 20m
            },
            CancellationToken.None);

        Assert.True(created.RequiresWholeInventoryUnits);
    }

    [Fact]
    public async Task CreateAsync_FractionalUnit_DoesNotForceWholeProductQuantity()
    {
        await using var dbContext = CreateDbContext();

        var category = new ProductCategory
        {
            Name = "Porcelanatos",
            CreatedAtUtc = DateTime.UtcNow
        };

        var unit = new UnitOfMeasure
        {
            Name = "Metro cuadrado",
            Symbol = "m²",
            RequiresWholeQuantity = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.AddRange(category, unit);
        await dbContext.SaveChangesAsync();

        var service = new ProductService(dbContext);

        var created = await service.CreateAsync(
            new ProductUpsertRequest
            {
                CategoryId = category.Id,
                Name = "Carrara",
                Description = "60x120",
                CabysCode = "1234567890123",
                InventoryUnitId = unit.Id,
                CommercialUnitId = unit.Id,
                CommercialUnitsPerInventoryUnit = 1m,
                RequiresWholeInventoryUnits = false,
                SalePrice = 15000m,
                CurrentCost = 10000m,
                TaxRate = 13m,
                MinimumStock = 5m
            },
            CancellationToken.None);

        Assert.False(created.RequiresWholeInventoryUnits);
    }

    private static RevestikDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<RevestikDbContext>()
                .UseInMemoryDatabase(
                    $"ProductUnitRule-{Guid.NewGuid()}")
                .Options;

        return new RevestikDbContext(options);
    }
}
