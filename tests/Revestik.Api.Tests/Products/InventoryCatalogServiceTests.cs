using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Services.Products;
using Revestik.Shared.Products;

namespace Revestik.Api.Tests.Products;

public sealed class InventoryCatalogServiceTests
{
    [Fact]
    public async Task CategoryLifecycle_NormalizesUpdatesAndDeactivates()
    {
        await using var dbContext = CreateDbContext();
        var service = new InventoryCatalogService(dbContext);

        var created = await service.CreateCategoryAsync(
            new ProductCategoryUpsertRequest
            {
                Name = "  Porcelanatos  "
            },
            CancellationToken.None);

        Assert.Equal("Porcelanatos", created.Name);
        Assert.True(created.IsActive);

        var updated = await service.UpdateCategoryAsync(
            created.Id,
            new ProductCategoryUpsertRequest
            {
                Name = "  Pisos y paredes  ",
                IsActive = true
            },
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Pisos y paredes", updated.Name);
        Assert.NotNull(updated.UpdatedAtUtc);

        Assert.True(await service.DeactivateCategoryAsync(
            created.Id,
            CancellationToken.None));

        Assert.Empty(await service.GetCategoriesAsync(
            includeInactive: false,
            CancellationToken.None));

        var all = await service.GetCategoriesAsync(
            includeInactive: true,
            CancellationToken.None);

        Assert.False(Assert.Single(all).IsActive);
    }

    [Fact]
    public async Task CreateCategory_WithDuplicateName_ThrowsConflictException()
    {
        await using var dbContext = CreateDbContext();
        var service = new InventoryCatalogService(dbContext);

        await service.CreateCategoryAsync(
            new ProductCategoryUpsertRequest
            {
                Name = "Adhesivos"
            },
            CancellationToken.None);

        await Assert.ThrowsAsync<DuplicateInventoryCatalogValueException>(
            () => service.CreateCategoryAsync(
                new ProductCategoryUpsertRequest
                {
                    Name = "Adhesivos"
                },
                CancellationToken.None));
    }

    [Fact]
    public async Task UnitLifecycle_NormalizesUpdatesAndDeactivates()
    {
        await using var dbContext = CreateDbContext();
        var service = new InventoryCatalogService(dbContext);

        var created = await service.CreateUnitAsync(
            new UnitOfMeasureUpsertRequest
            {
                Name = "  Metro cuadrado  ",
                Symbol = "  m²  "
            },
            CancellationToken.None);

        Assert.Equal("Metro cuadrado", created.Name);
        Assert.Equal("m²", created.Symbol);

        var updated = await service.UpdateUnitAsync(
            created.Id,
            new UnitOfMeasureUpsertRequest
            {
                Name = "Caja",
                Symbol = "caja",
                IsActive = true
            },
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Caja", updated.Name);
        Assert.Equal("caja", updated.Symbol);

        Assert.True(await service.DeactivateUnitAsync(
            created.Id,
            CancellationToken.None));

        Assert.Empty(await service.GetUnitsAsync(
            includeInactive: false,
            CancellationToken.None));

        Assert.False(Assert.Single(
            await service.GetUnitsAsync(
                includeInactive: true,
                CancellationToken.None)).IsActive);
    }

    [Theory]
    [InlineData("Caja", "otra")]
    [InlineData("Otra", "caja")]
    public async Task CreateUnit_WithDuplicateNameOrSymbol_ThrowsConflictException(
        string name,
        string symbol)
    {
        await using var dbContext = CreateDbContext();
        var service = new InventoryCatalogService(dbContext);

        await service.CreateUnitAsync(
            new UnitOfMeasureUpsertRequest
            {
                Name = "Caja",
                Symbol = "caja"
            },
            CancellationToken.None);

        await Assert.ThrowsAsync<DuplicateInventoryCatalogValueException>(
            () => service.CreateUnitAsync(
                new UnitOfMeasureUpsertRequest
                {
                    Name = name,
                    Symbol = symbol
                },
                CancellationToken.None));
    }

    [Fact]
    public async Task GetCatalogs_OrdersByNameThenId()
    {
        await using var dbContext = CreateDbContext();
        var service = new InventoryCatalogService(dbContext);

        await service.CreateCategoryAsync(
            new ProductCategoryUpsertRequest { Name = "Zócalos" },
            CancellationToken.None);
        await service.CreateCategoryAsync(
            new ProductCategoryUpsertRequest { Name = "Adhesivos" },
            CancellationToken.None);

        await service.CreateUnitAsync(
            new UnitOfMeasureUpsertRequest
            {
                Name = "Saco",
                Symbol = "saco"
            },
            CancellationToken.None);
        await service.CreateUnitAsync(
            new UnitOfMeasureUpsertRequest
            {
                Name = "Caja",
                Symbol = "caja"
            },
            CancellationToken.None);

        Assert.Equal(
            ["Adhesivos", "Zócalos"],
            (await service.GetCategoriesAsync(
                false,
                CancellationToken.None)).Select(item => item.Name));

        Assert.Equal(
            ["Caja", "Saco"],
            (await service.GetUnitsAsync(
                false,
                CancellationToken.None)).Select(item => item.Name));
    }

    private static RevestikDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase(
                $"InventoryCatalogTests-{Guid.NewGuid()}")
            .Options;

        return new RevestikDbContext(options);
    }
}