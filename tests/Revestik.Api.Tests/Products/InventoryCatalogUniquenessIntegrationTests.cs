using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.Products;

public sealed class InventoryCatalogUniquenessIntegrationTests(
    SqlServerIntegrationTestFixture sqlServerFixture)
    : IClassFixture<SqlServerIntegrationTestFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var dbContext = sqlServerFixture.CreateDbContext();

        await dbContext.Products.ExecuteDeleteAsync();
        await dbContext.ProductCategories.ExecuteDeleteAsync();
        await dbContext.UnitsOfMeasure.ExecuteDeleteAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ProductCategory_DuplicateName_IsRejectedBySqlServer()
    {
        await using var dbContext = sqlServerFixture.CreateDbContext();

        dbContext.ProductCategories.AddRange(
            CreateCategory("Porcelanatos"),
            CreateCategory("Porcelanatos"));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task UnitOfMeasure_DuplicateName_IsRejectedBySqlServer()
    {
        await using var dbContext = sqlServerFixture.CreateDbContext();

        dbContext.UnitsOfMeasure.AddRange(
            CreateUnit("Caja", "caja"),
            CreateUnit("Caja", "cj"));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task UnitOfMeasure_DuplicateSymbol_IsRejectedBySqlServer()
    {
        await using var dbContext = sqlServerFixture.CreateDbContext();

        dbContext.UnitsOfMeasure.AddRange(
            CreateUnit("Caja", "caja"),
            CreateUnit("Caja grande", "caja"));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());
    }

    private static ProductCategory CreateCategory(string name)
    {
        return new ProductCategory
        {
            Name = name,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static UnitOfMeasure CreateUnit(
        string name,
        string symbol)
    {
        return new UnitOfMeasure
        {
            Name = name,
            Symbol = symbol,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}