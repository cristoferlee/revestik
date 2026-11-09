using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Services.PublicCatalog;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicCatalogVisibilitySqlIntegrationTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private async Task<(int CategoryId, int SubcategoryId, int ProductId)> SeedAsync()
    {
        await using var db = fixture.CreateDbContext();
        var token = Guid.NewGuid().ToString("N");
        var category = new ProductCategory
        {
            Name = "Pisos-" + token, PublicSlug = "pisos-" + token,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var sub = new ProductSubcategory
        {
            CategoryId = category.Id, Name = "Porcelanatos-" + token,
            PublicSlug = "porcelanatos-" + token, CreatedAtUtc = DateTime.UtcNow
        };
        var product = new PublicCatalogProduct
        {
            Slug = "prod-" + token, IsPublished = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.ProductSubcategories.Add(sub);
        db.PublicCatalogProducts.Add(product);
        await db.SaveChangesAsync();
        db.PublicCatalogProductSubcategories.Add(new()
        {
            ProductId = product.Id, SubcategoryId = sub.Id
        });
        await db.SaveChangesAsync();
        return (category.Id, sub.Id, product.Id);
    }

    [Fact]
    public async Task PublishedProductWithValidSharedCategoryIsVisible()
    {
        var (_, _, productId) = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        Assert.True(await PublicCatalogVisibility.VisibleProducts(db).AnyAsync(x => x.Id == productId));
    }

    [Fact]
    public async Task InactiveInventorySubcategoryDoesNotHidePublicProduct()
    {
        var (_, subId, productId) = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        (await db.ProductSubcategories.SingleAsync(x => x.Id == subId)).IsActive = false;
        await db.SaveChangesAsync();
        Assert.True(await PublicCatalogVisibility.VisibleProducts(db).AnyAsync(x => x.Id == productId));
        Assert.True(await PublicCatalogVisibility.VisibleSubcategories(db).AnyAsync(x => x.Id == subId));
        Assert.True(await db.PublicCatalogProducts.Where(x => x.Id == productId)
            .Select(x => x.IsPublished).SingleAsync());
    }

    [Fact]
    public async Task InactiveInventoryCategoryDoesNotHidePublicCategoryOrProduct()
    {
        var (categoryId, _, productId) = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        (await db.ProductCategories.SingleAsync(x => x.Id == categoryId)).IsActive = false;
        await db.SaveChangesAsync();
        Assert.True(await PublicCatalogVisibility.VisibleCategories(db).AnyAsync(x => x.Id == categoryId));
        Assert.True(await PublicCatalogVisibility.VisibleProducts(db).AnyAsync(x => x.Id == productId));
    }

    [Fact]
    public async Task ArchivedProductIsHiddenEvenWithSharedClassification()
    {
        var (categoryId, subId, productId) = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var product = await db.PublicCatalogProducts.SingleAsync(x => x.Id == productId);
        product.IsPublished = false;
        product.IsArchived = true;
        await db.SaveChangesAsync();
        Assert.False(await PublicCatalogVisibility.VisibleProducts(db).AnyAsync(x => x.Id == productId));
        Assert.False(await PublicCatalogVisibility.VisibleCategories(db).AnyAsync(x => x.Id == categoryId));
        Assert.False(await PublicCatalogVisibility.VisibleSubcategories(db).AnyAsync(x => x.Id == subId));
    }

    [Fact]
    public async Task UnpublishedProductIsHiddenEvenWithActiveClassification()
    {
        var (_, _, productId) = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        (await db.PublicCatalogProducts.SingleAsync(x => x.Id == productId)).IsPublished = false;
        await db.SaveChangesAsync();
        Assert.False(await PublicCatalogVisibility.VisibleProducts(db).AnyAsync(x => x.Id == productId));
    }
}
