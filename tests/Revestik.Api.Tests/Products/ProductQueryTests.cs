using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.Products;
using Revestik.Shared.Products;

namespace Revestik.Api.Tests.Products;

public sealed class ProductQueryTests
{
    [Fact]
    public async Task GetPage_DefaultOrdering_IsNameAscending()
    {
        await using var db = CreateDbContext();
        await SeedAsync(db);

        var service = new ProductService(db);

        var result = await service.GetPageAsync(
            new ProductListRequest
            {
                ActivityStatus = ProductActivityStatus.All
            },
            CancellationToken.None);

        Assert.Equal(
            new[] { "Adhesivo", "Carrara", "Zeta Tool" },
            result.Items.Select(item => item.Name));
    }

    [Fact]
    public async Task GetPage_SortsByStockDescending()
    {
        await using var db = CreateDbContext();
        await SeedAsync(db);

        var service = new ProductService(db);

        var result = await service.GetPageAsync(
            new ProductListRequest
            {
                ActivityStatus = ProductActivityStatus.All,
                SortBy = ProductSortField.StockQuantity,
                SortDirection = SortDirection.Desc
            },
            CancellationToken.None);

        Assert.Equal(
            new[] { 20m, 5m, 0m },
            result.Items.Select(item => item.AvailableStock));
    }

    [Fact]
    public async Task GetPage_SearchByCabysAndStockFilter_Combine()
    {
        await using var db = CreateDbContext();
        await SeedAsync(db);

        var service = new ProductService(db);

        var result = await service.GetPageAsync(
            new ProductListRequest
            {
                Search = "1111111111111",
                ActivityStatus = ProductActivityStatus.All,
                StockStatus = ProductStockStatus.InStock
            },
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("Carrara", item.Name);
    }

    [Fact]
    public async Task GetPage_PaginatesAfterOrdering()
    {
        await using var db = CreateDbContext();
        await SeedAsync(db);

        var service = new ProductService(db);

        var result = await service.GetPageAsync(
            new ProductListRequest
            {
                ActivityStatus = ProductActivityStatus.All,
                SortBy = ProductSortField.Name,
                SortDirection = SortDirection.Asc,
                Page = 2,
                PageSize = 1
            },
            CancellationToken.None);

        Assert.Equal(3, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal("Carrara", item.Name);
    }

    private static RevestikDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"product-query-{Guid.NewGuid()}")
            .Options;

        return new RevestikDbContext(options);
    }

    private static async Task SeedAsync(RevestikDbContext db)
    {
        var categoryA = new ProductCategory
        {
            Name = "Porcelanato",
            CreatedAtUtc = DateTime.UtcNow
        };

        var categoryB = new ProductCategory
        {
            Name = "Herramientas",
            CreatedAtUtc = DateTime.UtcNow
        };

        var unit = new UnitOfMeasure
        {
            Name = "Unidad",
            Symbol = "und",
            CreatedAtUtc = DateTime.UtcNow
        };

        db.AddRange(categoryA, categoryB, unit);
        await db.SaveChangesAsync();

        db.Products.AddRange(
            CreateProduct(
                categoryA.Id,
                unit.Id,
                "Carrara",
                "1111111111111",
                20m,
                5m,
                10000m,
                15000m),
            CreateProduct(
                categoryA.Id,
                unit.Id,
                "Adhesivo",
                "1111111111111",
                5m,
                5m,
                3000m,
                5000m),
            CreateProduct(
                categoryB.Id,
                unit.Id,
                "Zeta Tool",
                "2222222222222",
                0m,
                2m,
                7000m,
                9000m));

        await db.SaveChangesAsync();
    }

    private static Product CreateProduct(
        int categoryId,
        int unitId,
        string name,
        string cabys,
        decimal stock,
        decimal minimumStock,
        decimal cost,
        decimal salePrice)
    {
        return new Product
        {
            CategoryId = categoryId,
            Name = name,
            Description = name,
            CabysCode = cabys,
            InventoryUnitId = unitId,
            CommercialUnitId = unitId,
            CommercialUnitsPerInventoryUnit = 1m,
            RequiresWholeInventoryUnits = true,
            SalePrice = salePrice,
            CurrentCost = cost,
            TaxRate = 13m,
            StockQuantity = stock,
            MinimumStock = minimumStock,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}