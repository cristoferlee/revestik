using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.PublicCatalog;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicCatalogModelTests
{
    private static RevestikDbContext Db() => new(new DbContextOptionsBuilder<RevestikDbContext>()
        .UseSqlServer("Server=(local);Database=RevestikModelOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);

    [Fact]
    public void TaxonomyIsSharedWithInternalInventory()
    {
        using var db = Db();
        Assert.DoesNotContain(db.Model.GetEntityTypes(), e => e.GetTableName() == "PublicCatalogCategories");
        Assert.DoesNotContain(db.Model.GetEntityTypes(), e => e.GetTableName() == "PublicCatalogSubcategories");
        Assert.DoesNotContain(db.Model.GetEntityTypes(), e => e.GetTableName() == "PublicCategoryBindings");
        Assert.DoesNotContain(db.Model.GetEntityTypes(), e => e.GetTableName() == "PublicSubcategoryBindings");
        var link = db.Model.FindEntityType(typeof(PublicCatalogProductSubcategory))!;
        Assert.Contains(link.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(ProductSubcategory));
        var categoryImage = db.Model.FindEntityType(typeof(PublicCatalogImage))!;
        Assert.Contains(categoryImage.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(ProductCategory));
    }

    [Fact]
    public void PublicProductsRemainIndependentOfInventoryProducts()
    {
        using var db = Db();
        var product = db.Model.FindEntityType(typeof(PublicCatalogProduct))!;
        Assert.DoesNotContain(product.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(Product));
    }

    [Fact]
    public void UniqueSharedCategorySlugAndScopedSubcategorySlug()
    {
        using var db = Db();
        var category = db.Model.FindEntityType(typeof(ProductCategory))!;
        Assert.Contains(category.GetIndexes(), i => i.IsUnique && i.Properties.Select(x => x.Name)
            .SequenceEqual(new[] { "PublicSlug" }));
        var sub = db.Model.FindEntityType(typeof(ProductSubcategory))!;
        Assert.Contains(sub.GetIndexes(), i => i.IsUnique && i.Properties.Select(x => x.Name)
            .SequenceEqual(new[] { "CategoryId", "PublicSlug" }));
    }

    [Fact]
    public void ProductSubcategoryAndTranslationKeysPreventDuplicates()
    {
        using var db = Db();
        var link = db.Model.FindEntityType(typeof(PublicCatalogProductSubcategory))!;
        Assert.Equal(new[] { "ProductId", "SubcategoryId" }, link.FindPrimaryKey()!.Properties.Select(x => x.Name));
        var translation = db.Model.FindEntityType(typeof(PublicCatalogProductTranslation))!;
        Assert.Equal(new[] { "ProductId", "LanguageCode" }, translation.FindPrimaryKey()!.Properties.Select(x => x.Name));
    }

    [Fact]
    public void PriceChecksAndRowVersionRemainMapped()
    {
        using var db = Db();
        var model = db.GetService<IDesignTimeModel>().Model;
        var product = model.FindEntityType(typeof(PublicCatalogProduct))!;
        Assert.Contains(product.GetCheckConstraints(), c => c.Name == "CK_PublicCatalogProducts_PriceFields");
        Assert.True(product.FindProperty("RowVersion")!.IsConcurrencyToken);
    }
}
