using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.Products;

public sealed class ProductDataIntegrityIntegrationTests(
    SqlServerIntegrationTestFixture sqlServerFixture)
    : IClassFixture<SqlServerIntegrationTestFixture>, IAsyncLifetime
{
    private int categoryId;
    private int inventoryUnitId;
    private int commercialUnitId;

    public async Task InitializeAsync()
    {
        await using var dbContext = sqlServerFixture.CreateDbContext();

        await dbContext.Products.ExecuteDeleteAsync();
        await dbContext.ProductCategories.ExecuteDeleteAsync();
        await dbContext.UnitsOfMeasure.ExecuteDeleteAsync();

        var category = new ProductCategory
        {
            Name = "Porcelanatos",
            CreatedAtUtc = DateTime.UtcNow
        };
        var inventoryUnit = new UnitOfMeasure
        {
            Name = "Caja",
            Symbol = "caja",
            CreatedAtUtc = DateTime.UtcNow
        };
        var commercialUnit = new UnitOfMeasure
        {
            Name = "Metro cuadrado",
            Symbol = "m²",
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.AddRange(category, inventoryUnit, commercialUnit);
        await dbContext.SaveChangesAsync();

        categoryId = category.Id;
        inventoryUnitId = inventoryUnit.Id;
        commercialUnitId = commercialUnit.Id;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Product_WithInvalidCabys_IsRejectedBySqlServer()
    {
        await AssertProductIsRejectedAsync(product =>
            product.CabysCode = "INVALID-CABYS");
    }

    [Fact]
    public async Task Product_WithNegativeStock_IsRejectedBySqlServer()
    {
        await AssertProductIsRejectedAsync(product =>
            product.StockQuantity = -1m);
    }

    [Fact]
    public async Task WholeUnitProduct_WithFractionalStock_IsRejectedBySqlServer()
    {
        await AssertProductIsRejectedAsync(product =>
        {
            product.RequiresWholeInventoryUnits = true;
            product.StockQuantity = 1.5m;
        });
    }

    private async Task AssertProductIsRejectedAsync(
        Action<Product> makeInvalid)
    {
        await using var dbContext = sqlServerFixture.CreateDbContext();
        var product = CreateValidProduct();
        makeInvalid(product);

        dbContext.Products.Add(product);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());
    }

    private Product CreateValidProduct()
    {
        return new Product
        {
            CategoryId = categoryId,
            Name = "Porcelanato Blanco 60x120",
            Description = "Porcelanato rectificado.",
            CabysCode = "1234567890123",
            InventoryUnitId = inventoryUnitId,
            CommercialUnitId = commercialUnitId,
            CommercialUnitsPerInventoryUnit = 1.44m,
            RequiresWholeInventoryUnits = false,
            SalePrice = 15000m,
            CurrentCost = 10000m,
            TaxRate = 13m,
            StockQuantity = 0m,
            MinimumStock = 5m,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}