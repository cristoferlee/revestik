using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.Products;
using Revestik.Shared.Products;

namespace Revestik.Api.Tests.Products;

public sealed class ProductServiceLifecycleTests
{
    [Fact]
    public async Task CreateUpdateDeleteAndReactivate_PreservesStock()
    {
        await using var dbContext = CreateDbContext();
        var catalog = await SeedCatalogAsync(dbContext);
        var service = new ProductService(dbContext);

        var created = await service.CreateAsync(
            CreateRequest(catalog),
            CancellationToken.None);

        Assert.Equal(0m, created.StockQuantity);
        Assert.False(created.IsDeleted);
        Assert.Equal("Porcelanatos", created.CategoryName);
        Assert.Equal("caja", created.InventoryUnitSymbol);
        Assert.Equal("m²", created.CommercialUnitSymbol);

        var product = await dbContext.Products.SingleAsync();
        product.StockQuantity = 12m;
        await dbContext.SaveChangesAsync();

        var updateRequest = CreateRequest(catalog);
        updateRequest.Name = "  Carrara Blanco 60x120  ";
        updateRequest.Description = "  Acabado brillante.  ";
        updateRequest.SalePrice = 17500m;

        var updated = await service.UpdateAsync(
            created.Id,
            updateRequest,
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Carrara Blanco 60x120", updated.Name);
        Assert.Equal("Acabado brillante.", updated.Description);
        Assert.Equal(17500m, updated.SalePrice);
        Assert.Equal(12m, updated.StockQuantity);
        Assert.NotNull(updated.UpdatedAtUtc);

        Assert.True(await service.DeleteAsync(
            created.Id,
            CancellationToken.None));
        Assert.True((await service.GetByIdAsync(
            created.Id,
            CancellationToken.None))!.IsDeleted);

        Assert.True(await service.ReactivateAsync(
            created.Id,
            CancellationToken.None));
        Assert.False((await service.GetByIdAsync(
            created.Id,
            CancellationToken.None))!.IsDeleted);
    }

    [Fact]
    public async Task Create_WithInactiveCategory_ThrowsCatalogException()
    {
        await using var dbContext = CreateDbContext();
        var catalog = await SeedCatalogAsync(dbContext);
        catalog.Category.IsActive = false;
        await dbContext.SaveChangesAsync();

        var service = new ProductService(dbContext);

        await Assert.ThrowsAsync<InvalidProductCatalogReferenceException>(
            () => service.CreateAsync(
                CreateRequest(catalog),
                CancellationToken.None));
    }

    [Fact]
    public async Task Update_CanRetainItsExistingInactiveCatalogValues()
    {
        await using var dbContext = CreateDbContext();
        var catalog = await SeedCatalogAsync(dbContext);
        var service = new ProductService(dbContext);

        var created = await service.CreateAsync(
            CreateRequest(catalog),
            CancellationToken.None);

        catalog.Category.IsActive = false;
        catalog.InventoryUnit.IsActive = false;
        catalog.CommercialUnit.IsActive = false;
        await dbContext.SaveChangesAsync();

        var request = CreateRequest(catalog);
        request.Name = "Producto actualizado";

        var updated = await service.UpdateAsync(
            created.Id,
            request,
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Producto actualizado", updated.Name);
    }

    [Fact]
    public async Task GetPage_WithFilters_ReturnsOnlyMatchingProduct()
    {
        await using var dbContext = CreateDbContext();
        var catalog = await SeedCatalogAsync(dbContext);

        dbContext.Products.AddRange(
            CreateProduct(
                catalog,
                "Disponible",
                "1111111111111",
                stock: 20m,
                minimum: 5m),
            CreateProduct(
                catalog,
                "Stock bajo",
                "2222222222222",
                stock: 3m,
                minimum: 5m),
            CreateProduct(
                catalog,
                "Sin stock",
                "3333333333333",
                stock: 0m,
                minimum: 5m),
            CreateProduct(
                catalog,
                "Eliminado",
                "4444444444444",
                stock: 0m,
                minimum: 5m,
                isDeleted: true));

        await dbContext.SaveChangesAsync();

        var service = new ProductService(dbContext);

        var lowStock = await service.GetPageAsync(
            new ProductListRequest
            {
                CategoryId = catalog.Category.Id,
                UnitId = catalog.CommercialUnit.Id,
                StockStatus = ProductStockStatus.LowStock
            },
            CancellationToken.None);

        Assert.Equal("Stock bajo", Assert.Single(lowStock.Items).Name);

        var deleted = await service.GetPageAsync(
            new ProductListRequest
            {
                ActivityStatus = ProductActivityStatus.Deleted
            },
            CancellationToken.None);

        Assert.Equal("Eliminado", Assert.Single(deleted.Items).Name);
    }

    private static RevestikDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase(
                $"ProductLifecycleTests-{Guid.NewGuid()}")
            .Options;

        return new RevestikDbContext(options);
    }

    private static async Task<CatalogFixture> SeedCatalogAsync(
        RevestikDbContext dbContext)
    {
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

        return new CatalogFixture(
            category,
            inventoryUnit,
            commercialUnit);
    }

    private static ProductUpsertRequest CreateRequest(CatalogFixture catalog)
    {
        return new ProductUpsertRequest
        {
            CategoryId = catalog.Category.Id,
            Name = "  Porcelanato Blanco 60x120  ",
            Description = "  Porcelanato rectificado.  ",
            CabysCode = "1234567890123",
            InventoryUnitId = catalog.InventoryUnit.Id,
            CommercialUnitId = catalog.CommercialUnit.Id,
            CommercialUnitsPerInventoryUnit = 1.44m,
            RequiresWholeInventoryUnits = true,
            SalePrice = 15000m,
            CurrentCost = 10000m,
            TaxRate = 13m,
            MinimumStock = 5m
        };
    }

    private static Product CreateProduct(
        CatalogFixture catalog,
        string name,
        string cabysCode,
        decimal stock,
        decimal minimum,
        bool isDeleted = false)
    {
        return new Product
        {
            CategoryId = catalog.Category.Id,
            Name = name,
            Description = $"Descripción de {name}",
            CabysCode = cabysCode,
            InventoryUnitId = catalog.InventoryUnit.Id,
            CommercialUnitId = catalog.CommercialUnit.Id,
            CommercialUnitsPerInventoryUnit = 1.44m,
            RequiresWholeInventoryUnits = true,
            SalePrice = 15000m,
            CurrentCost = 10000m,
            TaxRate = 13m,
            StockQuantity = stock,
            MinimumStock = minimum,
            IsDeleted = isDeleted,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private sealed record CatalogFixture(
        ProductCategory Category,
        UnitOfMeasure InventoryUnit,
        UnitOfMeasure CommercialUnit);
}