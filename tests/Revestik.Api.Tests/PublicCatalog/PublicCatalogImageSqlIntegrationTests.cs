using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.PublicCatalog;

/// <summary>
/// SQL Server coverage of constraints created by AddPublicCatalogImages.
/// Application-only rules (maximum five photos, fallback role/owner, publication
/// requirements) are deliberately not claimed as SQL constraints here.
/// </summary>
public sealed class PublicCatalogImageSqlIntegrationTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private static PublicCatalogCategory NewCategory() => new()
    {
        Slug = "cat-" + Guid.NewGuid().ToString("N"),
        NameEs = "Pisos interiores",
        CreatedAtUtc = DateTime.UtcNow
    };

    private static PublicCatalogProduct NewProduct() => new()
    {
        Slug = "prod-" + Guid.NewGuid().ToString("N"),
        CreatedAtUtc = DateTime.UtcNow
    };

    private static PublicCatalogImage Image(string role, string contentType = "image/jpeg") => new()
    {
        Role = role,
        ContentType = contentType,
        StorageKey = Guid.NewGuid().ToString("N"),
        Width = 900,
        Height = 600,
        FileSizeBytes = 10000,
        CreatedAtUtc = DateTime.UtcNow
    };

    [Fact]
    public async Task MigrationCreatesImagesTable()
    {
        await using var db = fixture.CreateDbContext();
        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, m => m.EndsWith("_AddPublicCatalogImages", StringComparison.Ordinal));
        var table = await db.Database.SqlQueryRaw<string>(
            "SELECT [name] AS [Value] FROM sys.tables WHERE [name] = 'PublicCatalogImages'")
            .SingleAsync();
        Assert.Equal("PublicCatalogImages", table);
    }

    [Fact]
    public async Task ProductCoverAndGalleryCanCoexist()
    {
        await using var db = fixture.CreateDbContext();
        var product = NewProduct();
        db.PublicCatalogProducts.Add(product);
        await db.SaveChangesAsync();
        var cover = Image("ProductCover");
        var galleryA = Image("ProductGallery");
        var galleryB = Image("ProductGallery");
        cover.ProductId = product.Id;
        galleryA.ProductId = product.Id;
        galleryB.ProductId = product.Id;
        db.Set<PublicCatalogImage>().AddRange(cover, galleryA, galleryB);
        await db.SaveChangesAsync();
        Assert.Equal(3, await db.Set<PublicCatalogImage>().CountAsync(x => x.ProductId == product.Id));
    }

    [Fact]
    public async Task TwoCoversForSameProductAreRejected()
    {
        await using var db = fixture.CreateDbContext();
        var product = NewProduct();
        db.PublicCatalogProducts.Add(product);
        await db.SaveChangesAsync();
        var cover = Image("ProductCover");
        cover.ProductId = product.Id;
        db.Set<PublicCatalogImage>().Add(cover);
        await db.SaveChangesAsync();

        await using var second = fixture.CreateDbContext();
        var duplicate = Image("ProductCover");
        duplicate.ProductId = product.Id;
        second.Set<PublicCatalogImage>().Add(duplicate);
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task TwoHeroPrimaryImagesForSameCategoryAreRejected()
    {
        await using var db = fixture.CreateDbContext();
        var category = NewCategory();
        db.PublicCatalogCategories.Add(category);
        await db.SaveChangesAsync();
        var hero = Image("CategoryHeroPrimary");
        hero.CategoryId = category.Id;
        db.Set<PublicCatalogImage>().Add(hero);
        await db.SaveChangesAsync();

        await using var second = fixture.CreateDbContext();
        var duplicate = Image("CategoryHeroPrimary");
        duplicate.CategoryId = category.Id;
        second.Set<PublicCatalogImage>().Add(duplicate);
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task GifHeroWithValidFallbackReferenceIsAccepted()
    {
        await using var db = fixture.CreateDbContext();
        var category = NewCategory();
        db.PublicCatalogCategories.Add(category);
        await db.SaveChangesAsync();
        var fallback = Image("CategoryHeroFallback");
        fallback.CategoryId = category.Id;
        db.Set<PublicCatalogImage>().Add(fallback);
        await db.SaveChangesAsync();
        var hero = Image("CategoryHeroPrimary", "image/gif");
        hero.CategoryId = category.Id;
        hero.FallbackImageId = fallback.Id;
        db.Set<PublicCatalogImage>().Add(hero);
        await db.SaveChangesAsync();
        Assert.True(hero.Id > 0);
    }

    [Theory]
    [InlineData("ProductCover", "image/gif", true, false)]
    [InlineData("CategoryGallery", "image/gif", false, true)]
    [InlineData("CategoryHeroPrimary", "image/gif", false, true)]
    [InlineData("ProductGallery", "image/svg+xml", true, false)]
    public async Task InvalidMimeRoleOrMissingGifFallbackRejected(
        string role, string mime, bool isProduct, bool isCategory)
    {
        await using var db = fixture.CreateDbContext();
        var category = NewCategory();
        var product = NewProduct();
        db.PublicCatalogCategories.Add(category);
        db.PublicCatalogProducts.Add(product);
        await db.SaveChangesAsync();
        var image = Image(role, mime);
        if (isProduct) image.ProductId = product.Id;
        if (isCategory) image.CategoryId = category.Id;
        db.Set<PublicCatalogImage>().Add(image);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData(0L, 800, 600)]
    [InlineData(5242881L, 800, 600)]
    [InlineData(1000L, 0, 600)]
    [InlineData(1000L, 9000, 600)]
    public async Task InvalidSizeOrDimensionsRejected(long bytes, int width, int height)
    {
        await using var db = fixture.CreateDbContext();
        var product = NewProduct();
        db.PublicCatalogProducts.Add(product);
        await db.SaveChangesAsync();
        var image = Image("ProductCover");
        image.ProductId = product.Id;
        image.FileSizeBytes = bytes;
        image.Width = width;
        image.Height = height;
        db.Set<PublicCatalogImage>().Add(image);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ImageCannotBelongToTwoOwners()
    {
        await using var db = fixture.CreateDbContext();
        var category = NewCategory();
        var product = NewProduct();
        db.PublicCatalogCategories.Add(category);
        db.PublicCatalogProducts.Add(product);
        await db.SaveChangesAsync();
        var image = Image("ProductCover");
        image.ProductId = product.Id;
        image.CategoryId = category.Id;
        db.Set<PublicCatalogImage>().Add(image);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
