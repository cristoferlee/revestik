using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.PublicCatalog;

// Reuse the project's existing SQL Server Testcontainers fixture; this class owns
// a temporary database. No local SQL Express or Azure database is modified.
public sealed class PublicCatalogSqlIntegrationTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private static string UniqueSlug(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}";

    private static PublicCatalogCategory Category(string? slug = null) => new()
    {
        Slug = slug ?? UniqueSlug("category"),
        NameEs = "Pisos exteriores",
        CreatedAtUtc = DateTime.UtcNow
    };

    private static PublicCatalogSubcategory Subcategory(int categoryId, string? slug = null) => new()
    {
        CategoryId = categoryId,
        Slug = slug ?? UniqueSlug("subcategory"),
        NameEs = "Porcelanatos",
        CreatedAtUtc = DateTime.UtcNow
    };

    private static PublicCatalogProduct Product(string? slug = null) => new()
    {
        Slug = slug ?? UniqueSlug("product"),
        CreatedAtUtc = DateTime.UtcNow
    };

    [Fact]
    public async Task MigrationCreatesFiveCatalogTables()
    {
        await using var db = fixture.CreateDbContext();
        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, x => x.EndsWith("_AddPublicCatalogFoundation", StringComparison.Ordinal));

        var tables = await db.Database.SqlQueryRaw<string>(
            "SELECT [name] AS [Value] FROM sys.tables WHERE [name] LIKE 'PublicCatalog%'")
            .ToListAsync();

        Assert.Equal(5, tables.Count);
        Assert.Contains("PublicCatalogCategories", tables);
        Assert.Contains("PublicCatalogSubcategories", tables);
        Assert.Contains("PublicCatalogProducts", tables);
        Assert.Contains("PublicCatalogProductSubcategories", tables);
        Assert.Contains("PublicCatalogProductTranslations", tables);
    }

    [Fact]
    public async Task ProductCanBelongToSubcategoriesAcrossTwoCategories()
    {
        await using var db = fixture.CreateDbContext();
        var outside = Category();
        var inside = Category();
        db.PublicCatalogCategories.AddRange(outside, inside);
        await db.SaveChangesAsync();

        var outdoors = Subcategory(outside.Id);
        var indoors = Subcategory(inside.Id);
        var product = Product();
        db.PublicCatalogSubcategories.AddRange(outdoors, indoors);
        db.PublicCatalogProducts.Add(product);
        await db.SaveChangesAsync();

        db.PublicCatalogProductSubcategories.AddRange(
            new PublicCatalogProductSubcategory { ProductId = product.Id, SubcategoryId = outdoors.Id },
            new PublicCatalogProductSubcategory { ProductId = product.Id, SubcategoryId = indoors.Id });
        await db.SaveChangesAsync();

        await using var verify = fixture.CreateDbContext();
        var actualCategoryIds = await verify.PublicCatalogProductSubcategories
            .AsNoTracking()
            .Where(x => x.ProductId == product.Id)
            .Select(x => x.Subcategory.CategoryId)
            .ToListAsync();
        Assert.Equal(2, actualCategoryIds.Distinct().Count());
        Assert.Contains(outside.Id, actualCategoryIds);
        Assert.Contains(inside.Id, actualCategoryIds);
    }

    [Fact]
    public async Task DuplicateProductMembershipIsRejectedBySqlServer()
    {
        await using var db = fixture.CreateDbContext();
        var category = Category();
        db.PublicCatalogCategories.Add(category);
        var product = Product();
        db.PublicCatalogProducts.Add(product);
        await db.SaveChangesAsync();
        var sub = Subcategory(category.Id);
        db.PublicCatalogSubcategories.Add(sub);
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
        db.PublicCatalogSubcategories.Add(Subcategory(int.MaxValue));
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
            db.PublicCatalogCategories.Add(Category(sharedCategorySlug));
            await db.SaveChangesAsync();
        }
        await using (var duplicate = fixture.CreateDbContext())
        {
            duplicate.PublicCatalogCategories.Add(Category(sharedCategorySlug));
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
            db.PublicCatalogCategories.AddRange(a, b);
            await db.SaveChangesAsync();
            var sharedSubSlug = UniqueSlug("sub");
            db.PublicCatalogSubcategories.AddRange(
                Subcategory(a.Id, sharedSubSlug),
                Subcategory(b.Id, sharedSubSlug));
            await db.SaveChangesAsync(); // Same slug in different categories is permitted.

            db.PublicCatalogSubcategories.Add(Subcategory(a.Id, sharedSubSlug));
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
            db.PublicCatalogCategories.Add(category);
            await db.SaveChangesAsync();
            var subcategory = Subcategory(category.Id);
            db.PublicCatalogSubcategories.Add(subcategory);
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.PublicCatalogCategories.AnyAsync(x => x.Slug == slug));
    }

    [Fact]
    public async Task ReferencedCategoryCannotBePhysicallyDeleted()
    {
        int categoryId;
        await using (var db = fixture.CreateDbContext())
        {
            var category = Category();
            db.PublicCatalogCategories.Add(category);
            await db.SaveChangesAsync();
            db.PublicCatalogSubcategories.Add(Subcategory(category.Id));
            await db.SaveChangesAsync();
            categoryId = category.Id;
        }

        await using var attempt = fixture.CreateDbContext();
        // ExecuteDelete bypasses EF's tracked relationship fix-up: SQL FK must reject this.
        await Assert.ThrowsAsync<SqlException>(async () =>
            await attempt.PublicCatalogCategories.Where(x => x.Id == categoryId)
                .ExecuteDeleteAsync());
    }
}
