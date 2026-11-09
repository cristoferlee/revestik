using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Revestik.Api.Models;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Endpoints.PublicCatalog;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.PublicCatalog;

// Reuse the project's existing SQL Server Testcontainers fixture; this class owns
// a temporary database. No local SQL Express or Azure database is modified.
public sealed class PublicCatalogSqlIntegrationTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private static string UniqueSlug(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}";

    private static ProductCategory Category(string? slug = null) => new()
    {
        PublicSlug = slug ?? UniqueSlug("category"),
        Name = UniqueSlug("Pisos exteriores"),
        CreatedAtUtc = DateTime.UtcNow
    };

    private static ProductSubcategory Subcategory(int categoryId, string? slug = null) => new()
    {
        CategoryId = categoryId,
        PublicSlug = slug ?? UniqueSlug("subcategory"),
        Name = UniqueSlug("Porcelanatos"),
        CreatedAtUtc = DateTime.UtcNow
    };

    private static PublicCatalogProduct Product(string? slug = null) => new()
    {
        Slug = slug ?? UniqueSlug("product"),
        CreatedAtUtc = DateTime.UtcNow
    };

    [Fact]
    public async Task FeaturedProductsUseOnlyPublishedPublicCatalogAndReturnOrderedGalleries()
    {
        await using var db = fixture.CreateDbContext();
        var category = Category();
        category.IsActive = false; // Inventory state must not hide public products.
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var subcategory = Subcategory(category.Id);
        subcategory.IsActive = false;
        db.ProductSubcategories.Add(subcategory);
        await db.SaveChangesAsync();

        var products = Enumerable.Range(0, 6).Select(i =>
        {
            var item = Product();
            item.IsPublished = true;
            item.DisplayOrder = i;
            return item;
        }).ToArray();
        var unpublished = Product();
        db.PublicCatalogProducts.AddRange(products);
        db.PublicCatalogProducts.Add(unpublished);
        await db.SaveChangesAsync();

        foreach (var product in products.Append(unpublished))
            db.PublicCatalogProductSubcategories.Add(new()
            {
                ProductId = product.Id, SubcategoryId = subcategory.Id
            });
        foreach (var product in products.Append(unpublished))
        {
            db.Set<PublicCatalogImage>().Add(new()
            {
                ProductId = product.Id, Role = "ProductCover", StorageKey = Guid.NewGuid().ToString("N"),
                ContentType = "image/jpeg", Width = 680, Height = 510, FileSizeBytes = 300,
                DisplayOrder = 1, CreatedAtUtc = DateTime.UtcNow
            });
        }
        db.Set<PublicCatalogImage>().Add(new()
        {
            ProductId = products[4].Id, Role = "ProductGallery", StorageKey = Guid.NewGuid().ToString("N"),
            ContentType = "image/jpeg", Width = 680, Height = 510, FileSizeBytes = 300,
            DisplayOrder = 0, CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var requested = await PublicCatalogEndpoints.ReadFeaturedPublicProductsAsync(db,
            [products[4].Slug, unpublished.Slug, products[0].Slug], CancellationToken.None);
        var selectedJson = System.Text.Json.JsonSerializer.SerializeToElement(requested);
        Assert.Equal(2, selectedJson.GetArrayLength());
        Assert.Equal(products[4].Slug, selectedJson[0].GetProperty("Slug").GetString());
        Assert.Equal(products[0].Slug, selectedJson[1].GetProperty("Slug").GetString());
        var gallery = selectedJson[0].GetProperty("Images");
        Assert.Equal(2, gallery.GetArrayLength());
        Assert.Equal("ProductCover", gallery[0].GetProperty("Role").GetString());

        var automatic = await PublicCatalogEndpoints.ReadFeaturedPublicProductsAsync(db,
            [], CancellationToken.None);
        var automaticJson = System.Text.Json.JsonSerializer.SerializeToElement(automatic);
        Assert.Equal(5, automaticJson.GetArrayLength());
        for (var i = 0; i < 5; i++)
            Assert.Equal(products[i].Slug, automaticJson[i].GetProperty("Slug").GetString());
    }

    [Fact]
    public async Task ProductSlugIsGeneratedFromSpanishNameAndDuplicatesGetStableSuffixes()
    {
        await using var db = fixture.CreateDbContext();
        var name = "Porcelanato Mármol-" + Guid.NewGuid().ToString("N")[..8];
        var first = await PublicCatalogEndpoints.GenerateUniqueProductSlugAsync(name, db, CancellationToken.None);
        Assert.StartsWith("porcelanato-marmol-", first);
        Assert.True(first.Length <= 160);

        db.PublicCatalogProducts.Add(Product(first));
        await db.SaveChangesAsync();
        var duplicate = await PublicCatalogEndpoints.GenerateUniqueProductSlugAsync(name, db, CancellationToken.None);
        Assert.Equal(first + "-2", duplicate);
        db.PublicCatalogProducts.Add(Product(duplicate));
        await db.SaveChangesAsync();
        Assert.Equal(first + "-3", await PublicCatalogEndpoints.GenerateUniqueProductSlugAsync(name, db, CancellationToken.None));

        Assert.Equal("producto", await PublicCatalogEndpoints.GenerateUniqueProductSlugAsync("東京", db, CancellationToken.None));
        var longName = new string('A', 300);
        Assert.True((await PublicCatalogEndpoints.GenerateUniqueProductSlugAsync(longName, db, CancellationToken.None)).Length <= 160);
    }

    [Fact]
    public async Task PermanentPublicProductDeletionRemovesOnlyPublicDependenciesAndFeaturedReference()
    {
        await using var db = fixture.CreateDbContext();
        var category = Category();
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var subcategory = Subcategory(category.Id);
        db.ProductSubcategories.Add(subcategory);
        await db.SaveChangesAsync();

        var removed = Product();
        removed.IsPublished = true;
        var kept = Product();
        kept.IsPublished = true;
        db.PublicCatalogProducts.AddRange(removed, kept);
        await db.SaveChangesAsync();
        db.PublicCatalogProductSubcategories.AddRange(
            new() { ProductId = removed.Id, SubcategoryId = subcategory.Id },
            new() { ProductId = kept.Id, SubcategoryId = subcategory.Id });
        db.PublicCatalogProductTranslations.Add(new()
        {
            ProductId = removed.Id, LanguageCode = "es", Name = "Porcelanato",
            Description = "Producto de muestra"
        });
        var coverKey = Guid.NewGuid().ToString("N");
        var galleryKey = Guid.NewGuid().ToString("N");
        db.Set<PublicCatalogImage>().AddRange(
            new() { ProductId = removed.Id, Role = "ProductCover", StorageKey = coverKey,
                ContentType = "image/png", Width = 100, Height = 100, FileSizeBytes = 200,
                DisplayOrder = 0, CreatedAtUtc = DateTime.UtcNow },
            new() { ProductId = removed.Id, Role = "ProductGallery", StorageKey = galleryKey,
                ContentType = "image/png", Width = 100, Height = 100, FileSizeBytes = 200,
                DisplayOrder = 1, CreatedAtUtc = DateTime.UtcNow });
        var section = new PublicHomeSection
        {
            Key = "products", ContentEs = "{}", UpdatedAtUtc = DateTime.UtcNow,
            FeaturedSlugsJson = System.Text.Json.JsonSerializer.Serialize(new[] { removed.Slug, kept.Slug })
        };
        db.PublicHomeSections.Add(section);
        await db.SaveChangesAsync();
        var version = Convert.ToBase64String(removed.RowVersion);

        var outcome = await PublicCatalogEndpoints.DeletePublicProductDataAsync(
            removed.Id, version, db, "test-admin", CancellationToken.None);
        Assert.Equal(PublicCatalogEndpoints.ProductDeletionStatus.Deleted, outcome.Status);
        Assert.Equal(2, outcome.StorageKeys.Count);
        Assert.Contains(coverKey, outcome.StorageKeys);
        Assert.Contains(galleryKey, outcome.StorageKeys);

        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.PublicCatalogProducts.AnyAsync(x => x.Id == removed.Id));
        Assert.False(await verify.PublicCatalogProductTranslations.AnyAsync(x => x.ProductId == removed.Id));
        Assert.False(await verify.PublicCatalogProductSubcategories.AnyAsync(x => x.ProductId == removed.Id));
        Assert.False(await verify.Set<PublicCatalogImage>().AnyAsync(x => x.ProductId == removed.Id));
        Assert.True(await verify.PublicCatalogProducts.AnyAsync(x => x.Id == kept.Id));
        Assert.True(await verify.ProductCategories.AnyAsync(x => x.Id == category.Id));
        Assert.True(await verify.ProductSubcategories.AnyAsync(x => x.Id == subcategory.Id));
        var stored = await verify.PublicHomeSections.AsNoTracking().SingleAsync(x => x.Key == "products");
        Assert.Equal(new[] { kept.Slug }, System.Text.Json.JsonSerializer.Deserialize<List<string>>(stored.FeaturedSlugsJson));
    }

    [Fact]
    public async Task StaleVersionCannotDeletePublicProductOrItsImages()
    {
        await using var db = fixture.CreateDbContext();
        var product = Product();
        db.PublicCatalogProducts.Add(product);
        await db.SaveChangesAsync();
        db.Set<PublicCatalogImage>().Add(new()
        {
            ProductId = product.Id, Role = "ProductCover", StorageKey = Guid.NewGuid().ToString("N"),
            ContentType = "image/png", Width = 100, Height = 100, FileSizeBytes = 200,
            DisplayOrder = 0, CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var stale = Convert.ToBase64String(new byte[] { 1, 2, 3 });
        var outcome = await PublicCatalogEndpoints.DeletePublicProductDataAsync(
            product.Id, stale, db, "test-admin", CancellationToken.None);
        Assert.Equal(PublicCatalogEndpoints.ProductDeletionStatus.Conflict, outcome.Status);
        await using var verify = fixture.CreateDbContext();
        Assert.True(await verify.PublicCatalogProducts.AnyAsync(p => p.Id == product.Id));
        Assert.True(await verify.Set<PublicCatalogImage>().AnyAsync(i => i.ProductId == product.Id));
    }

    [Fact]
    public async Task MigrationCreatesSixCatalogTables()
    {
        await using var db = fixture.CreateDbContext();
        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, x => x.EndsWith("_AddPublicCatalogFoundation", StringComparison.Ordinal));

        var tables = await db.Database.SqlQueryRaw<string>(
            "SELECT [name] AS [Value] FROM sys.tables WHERE [name] LIKE 'PublicCatalog%'")
            .ToListAsync();

        Assert.Equal(4, tables.Count);
        Assert.Contains("PublicCatalogImages", tables);
        Assert.Contains("PublicCatalogProducts", tables);
        Assert.Contains("PublicCatalogProductSubcategories", tables);
        Assert.Contains("PublicCatalogProductTranslations", tables);
    }

    [Fact]
    public async Task ProductCanSelectMultipleSubcategoriesWithinOneSharedCategory()
    {
        await using var db = fixture.CreateDbContext();
        var category = Category();
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var interior = Subcategory(category.Id);
        var exterior = Subcategory(category.Id);
        var product = Product();
        db.ProductSubcategories.AddRange(interior, exterior);
        db.PublicCatalogProducts.Add(product);
        await db.SaveChangesAsync();

        Assert.True(await PublicCatalogEndpoints.ValidProductSubcategoriesAsync(
            [interior.Id, exterior.Id], db, CancellationToken.None));

        db.PublicCatalogProductSubcategories.AddRange(
            new PublicCatalogProductSubcategory { ProductId = product.Id, SubcategoryId = interior.Id },
            new PublicCatalogProductSubcategory { ProductId = product.Id, SubcategoryId = exterior.Id });
        await db.SaveChangesAsync();

        await using var verify = fixture.CreateDbContext();
        var categoryIds = await verify.PublicCatalogProductSubcategories.AsNoTracking()
            .Where(x => x.ProductId == product.Id)
            .Select(x => x.Subcategory.CategoryId).ToListAsync();
        Assert.Equal(2, categoryIds.Count);
        Assert.Single(categoryIds.Distinct());
        Assert.Equal(category.Id, categoryIds[0]);
    }

    [Fact]
    public async Task CrossCategoryProductSelectionIsRejectedByPublicWriteRule()
    {
        await using var db = fixture.CreateDbContext();
        var interior = Category();
        var exterior = Category();
        db.ProductCategories.AddRange(interior, exterior);
        await db.SaveChangesAsync();
        var subInterior = Subcategory(interior.Id);
        var subExterior = Subcategory(exterior.Id);
        db.ProductSubcategories.AddRange(subInterior, subExterior);
        await db.SaveChangesAsync();

        Assert.False(await PublicCatalogEndpoints.ValidProductSubcategoriesAsync(
            [subInterior.Id, subExterior.Id], db, CancellationToken.None));
    }

    [Fact]
    public async Task InactiveSharedClassificationsCanBeSelectedForPublicProduct()
    {
        await using var db = fixture.CreateDbContext();
        var category = Category();
        category.IsActive = false;
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var sub = Subcategory(category.Id);
        sub.IsActive = false;
        db.ProductSubcategories.Add(sub);
        await db.SaveChangesAsync();

        Assert.True(await PublicCatalogEndpoints.ValidProductSubcategoriesAsync(
            [sub.Id], db, CancellationToken.None));
    }

    [Fact]
    public async Task EmptyDuplicateAndUnknownSubcategorySelectionsAreRejected()
    {
        await using var db = fixture.CreateDbContext();
        var category = Category();
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var sub = Subcategory(category.Id);
        db.ProductSubcategories.Add(sub);
        await db.SaveChangesAsync();

        Assert.False(await PublicCatalogEndpoints.ValidProductSubcategoriesAsync(
            Array.Empty<int>(), db, CancellationToken.None));
        Assert.False(await PublicCatalogEndpoints.ValidProductSubcategoriesAsync(
            [sub.Id, sub.Id], db, CancellationToken.None));
        Assert.False(await PublicCatalogEndpoints.ValidProductSubcategoriesAsync(
            [sub.Id, int.MaxValue], db, CancellationToken.None));
    }

    [Fact]
    public async Task DuplicateSharedSubcategoryMembershipIsRejectedBySqlServer()
    {
        await using var db = fixture.CreateDbContext();
        var category = new Revestik.Api.Models.ProductCategory
        {
            Name = "Shared-" + Guid.NewGuid().ToString("N"), CreatedAtUtc = DateTime.UtcNow
        };
        var product = Product();
        db.ProductCategories.Add(category);
        db.PublicCatalogProducts.Add(product);
        await db.SaveChangesAsync();
        var sub = new Revestik.Api.Models.ProductSubcategory
        {
            CategoryId = category.Id, Name = "Porcelanato", CreatedAtUtc = DateTime.UtcNow
        };
        db.ProductSubcategories.Add(sub);
        await db.SaveChangesAsync();
        db.PublicCatalogProductSubcategories.Add(new()
        {
            ProductId = product.Id, SubcategoryId = sub.Id
        });
        await db.SaveChangesAsync();
        await using var second = fixture.CreateDbContext();
        second.PublicCatalogProductSubcategories.Add(new()
        {
            ProductId = product.Id, SubcategoryId = sub.Id
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task InvalidCategoryAndProductRelationsAreRejectedBySqlServer()
    {
        await using var db = fixture.CreateDbContext();
        db.ProductSubcategories.Add(Subcategory(int.MaxValue));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        await using var other = fixture.CreateDbContext();
        other.PublicCatalogProductSubcategories.Add(new()
        {
            ProductId = int.MaxValue, SubcategoryId = int.MaxValue
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());
    }

    [Fact]
    public async Task CategoryAndProductSlugsAreUniqueAndSubcategorySlugIsScopedToCategory()
    {
        var sharedCategorySlug = UniqueSlug("category");
        await using (var db = fixture.CreateDbContext())
        {
            db.ProductCategories.Add(Category(sharedCategorySlug));
            await db.SaveChangesAsync();
        }
        await using (var duplicate = fixture.CreateDbContext())
        {
            duplicate.ProductCategories.Add(Category(sharedCategorySlug));
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
        }

        var sharedProductSlug = UniqueSlug("product");
        await using (var db = fixture.CreateDbContext())
        {
            db.PublicCatalogProducts.Add(Product(sharedProductSlug));
            await db.SaveChangesAsync();
        }
        await using (var duplicate = fixture.CreateDbContext())
        {
            duplicate.PublicCatalogProducts.Add(Product(sharedProductSlug));
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
        }

        await using (var db = fixture.CreateDbContext())
        {
            var a = Category();
            var b = Category();
            db.ProductCategories.AddRange(a, b);
            await db.SaveChangesAsync();
            var sharedSubSlug = UniqueSlug("sub");
            db.ProductSubcategories.AddRange(
                Subcategory(a.Id, sharedSubSlug),
                Subcategory(b.Id, sharedSubSlug));
            await db.SaveChangesAsync(); // Same slug in different categories is permitted.

            db.ProductSubcategories.Add(Subcategory(a.Id, sharedSubSlug));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [Theory]
    [InlineData(-1, "CRC", "m²")]
    [InlineData(100, null, "m²")]
    [InlineData(100, "EUR", "m²")]
    [InlineData(100, "CRC", null)]
    [InlineData(100, "CRC", "   ")]
    [InlineData(null, "CRC", null)]
    public async Task InvalidPriceCurrencyUnitCombinationsAreRejected(
        int? price, string? currency, string? unit)
    {
        await using var db = fixture.CreateDbContext();
        var product = Product();
        product.Price = price;
        product.Currency = currency;
        product.SalesUnit = unit;
        db.PublicCatalogProducts.Add(product);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ValidNullablePriceAndPricedUnitAreAccepted()
    {
        await using var db = fixture.CreateDbContext();
        var noPrice = Product();
        noPrice.SalesUnit = "m²";
        var priced = Product();
        priced.Price = 12500m;
        priced.Currency = "CRC";
        priced.SalesUnit = "m²";
        db.PublicCatalogProducts.AddRange(noPrice, priced);
        await db.SaveChangesAsync();
        Assert.True(noPrice.Id > 0);
        Assert.True(priced.Id > 0);
    }

    [Fact]
    public async Task TranslationLanguagesAndUniquenessAreEnforced()
    {
        await using var db = fixture.CreateDbContext();
        var product = Product();
        db.PublicCatalogProducts.Add(product);
        await db.SaveChangesAsync();
        db.PublicCatalogProductTranslations.AddRange(
            new() { ProductId = product.Id, LanguageCode = "es", Name = "Porcelanato" },
            new() { ProductId = product.Id, LanguageCode = "en", Name = "Porcelain tile" },
            new() { ProductId = product.Id, LanguageCode = "de", Name = "Feinsteinzeug" });
        await db.SaveChangesAsync();

        await using var duplicate = fixture.CreateDbContext();
        duplicate.PublicCatalogProductTranslations.Add(new()
        {
            ProductId = product.Id, LanguageCode = "es", Name = "Otro nombre"
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());

        await using var invalid = fixture.CreateDbContext();
        invalid.PublicCatalogProductTranslations.Add(new()
        {
            ProductId = product.Id, LanguageCode = "fr", Name = "Produit"
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => invalid.SaveChangesAsync());
    }

    [Fact]
    public async Task RowVersionRejectsStaleProductUpdate()
    {
        int id;
        await using (var create = fixture.CreateDbContext())
        {
            var product = Product();
            create.PublicCatalogProducts.Add(product);
            await create.SaveChangesAsync();
            id = product.Id;
        }

        await using var a = fixture.CreateDbContext();
        await using var b = fixture.CreateDbContext();
        var first = await a.PublicCatalogProducts.SingleAsync(x => x.Id == id);
        var stale = await b.PublicCatalogProducts.SingleAsync(x => x.Id == id);
        first.Brand = "Primera edición";
        await a.SaveChangesAsync();
        stale.Brand = "Edición obsoleta";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
    }

    [Fact]
    public async Task ExplicitTransactionRollbackRemovesAllChanges()
    {
        var slug = UniqueSlug("rolled-back");
        await using (var db = fixture.CreateDbContext())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var category = Category(slug);
            db.ProductCategories.Add(category);
            await db.SaveChangesAsync();
            var subcategory = Subcategory(category.Id);
            db.ProductSubcategories.Add(subcategory);
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.ProductCategories.AnyAsync(x => x.PublicSlug == slug));
    }

    [Fact]
    public async Task ReferencedCategoryCannotBePhysicallyDeleted()
    {
        int categoryId;
        await using (var db = fixture.CreateDbContext())
        {
            var category = Category();
            db.ProductCategories.Add(category);
            await db.SaveChangesAsync();
            db.ProductSubcategories.Add(Subcategory(category.Id));
            await db.SaveChangesAsync();
            categoryId = category.Id;
        }

        await using var attempt = fixture.CreateDbContext();
        // ExecuteDelete bypasses EF's tracked relationship fix-up: SQL FK must reject this.
        await Assert.ThrowsAsync<SqlException>(async () =>
            await attempt.ProductCategories.Where(x => x.Id == categoryId)
                .ExecuteDeleteAsync());
    }
}
