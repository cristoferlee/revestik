using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.Products;
using Revestik.Shared.Products;

namespace Revestik.Api.Tests.Products;

public sealed class ProductServicePaginationTests
{
    [Fact]
    public async Task GetPageAsync_ExcludesDeletedProductsButIncludesZeroStock()
    {
        await using var dbContext = CreateDbContext();

        dbContext.Products.AddRange(
            CreateProduct(
                "Porcelanato Blanco",
                "1234567890123",
                stockQuantity: 25m),
            CreateProduct(
                "Porcelanato Sin Stock",
                "1234567890124",
                stockQuantity: 0m),
            CreateProduct(
                "Producto Eliminado",
                "1234567890125",
                stockQuantity: 10m,
                isDeleted: true));

        await dbContext.SaveChangesAsync();

        var service = new ProductService(dbContext);

        var result = await service.GetPageAsync(
            new ProductListRequest
            {
                Page = 1,
                PageSize = 20
            },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);

        Assert.Contains(
            result.Items,
            product =>
                product.Description == "Porcelanato Blanco");

        Assert.Contains(
            result.Items,
            product =>
                product.Description == "Porcelanato Sin Stock" &&
                product.StockQuantity == 0m);

        Assert.DoesNotContain(
            result.Items,
            product =>
                product.Description == "Producto Eliminado");
    }

    [Theory]
    [InlineData("Bondex", "Bondex Premium")]
    [InlineData("9876543210123", "Mortero Gris")]
    public async Task GetPageAsync_WithSearch_FiltersByDescriptionOrCabys(
        string search,
        string expectedDescription)
    {
        await using var dbContext = CreateDbContext();

        dbContext.Products.AddRange(
            CreateProduct(
                "Bondex Premium",
                "1234567890123",
                stockQuantity: 20m),
            CreateProduct(
                "Mortero Gris",
                "9876543210123",
                stockQuantity: 15m),
            CreateProduct(
                "Porcelanato Blanco",
                null,
                stockQuantity: 10m));

        await dbContext.SaveChangesAsync();

        var service = new ProductService(dbContext);

        var result = await service.GetPageAsync(
            new ProductListRequest
            {
                Search = search,
                Page = 1,
                PageSize = 20
            },
            CancellationToken.None);

        var product = Assert.Single(result.Items);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(expectedDescription, product.Description);
    }

    [Fact]
    public async Task GetPageAsync_WithSecondPage_ReturnsExpectedItemsAndMetadata()
    {
        await using var dbContext = CreateDbContext();

        dbContext.Products.AddRange(
            CreateProduct(
                "Adhesivo",
                "1111111111111",
                stockQuantity: 10m),
            CreateProduct(
                "Bondex",
                "2222222222222",
                stockQuantity: 20m),
            CreateProduct(
                "Mortero",
                "3333333333333",
                stockQuantity: 30m),
            CreateProduct(
                "Porcelanato",
                "4444444444444",
                stockQuantity: 40m),
            CreateProduct(
                "Sellador",
                "5555555555555",
                stockQuantity: 50m));

        await dbContext.SaveChangesAsync();

        var service = new ProductService(dbContext);

        var result = await service.GetPageAsync(
            new ProductListRequest
            {
                Page = 2,
                PageSize = 2
            },
            CancellationToken.None);

        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.Items.Count);

        Assert.Equal(
            ["Mortero", "Porcelanato"],
            result.Items.Select(product => product.Description));
    }

    [Fact]
    public async Task GetPageAsync_WithPageBeyondResults_ReturnsEmptyPage()
    {
        await using var dbContext = CreateDbContext();

        dbContext.Products.AddRange(
            CreateProduct(
                "Bondex",
                "1111111111111",
                stockQuantity: 10m),
            CreateProduct(
                "Mortero",
                "2222222222222",
                stockQuantity: 20m));

        await dbContext.SaveChangesAsync();

        var service = new ProductService(dbContext);

        var result = await service.GetPageAsync(
            new ProductListRequest
            {
                Page = int.MaxValue,
                PageSize = 100
            },
            CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(int.MaxValue, result.Page);
        Assert.Equal(100, result.PageSize);
    }

    private static RevestikDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase(
                $"ProductPaginationTests-{Guid.NewGuid()}")
            .Options;

        return new RevestikDbContext(options);
    }

    private static Product CreateProduct(
        string description,
        string? cabysCode,
        decimal stockQuantity,
        bool isDeleted = false)
    {
        return new Product
        {
            Description = description,
            CabysCode = cabysCode,
            Unit = "m²",
            SalePrice = 15000m,
            TaxRate = 13m,
            StockQuantity = stockQuantity,
            IsDeleted = isDeleted,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}