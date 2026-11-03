using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Revestik.Api.Data;
using Revestik.Api.Models.PublicCatalog;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicCatalogModelTests
{
    private static RevestikDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<RevestikDbContext>()
            .UseSqlServer("Server=(local);Database=RevestikCatalogModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);

    [Fact]
    public void CatalogHasFiveIsolatedTables()
    {
        using var db = CreateContext();
        var entityTypes = new[]
        {
            typeof(PublicCatalogCategory), typeof(PublicCatalogSubcategory),
            typeof(PublicCatalogProduct), typeof(PublicCatalogProductSubcategory),
            typeof(PublicCatalogProductTranslation)
        };

        foreach (var type in entityTypes)
        {
            var entity = db.Model.FindEntityType(type);
            Assert.NotNull(entity);
            Assert.StartsWith("PublicCatalog", entity.GetTableName());
        }
    }

    [Fact]
    public void ProductDoesNotDependOnOperationalInventory()
    {
        using var db = CreateContext();
        foreach (var type in db.Model.GetEntityTypes().Where(x =>
            x.ClrType.Namespace == typeof(PublicCatalogProduct).Namespace))
        {
            Assert.All(type.GetForeignKeys(), fk =>
                Assert.Equal(typeof(PublicCatalogProduct).Namespace, fk.PrincipalEntityType.ClrType.Namespace));
        }
    }

    [Fact]
    public void ProductSubcategoryAndTranslationKeysPreventDuplicates()
    {
        using var db = CreateContext();
        var links = db.Model.FindEntityType(typeof(PublicCatalogProductSubcategory))!;
        Assert.Equal(new[] { "ProductId", "SubcategoryId" },
            links.FindPrimaryKey()!.Properties.Select(x => x.Name));

        var translations = db.Model.FindEntityType(typeof(PublicCatalogProductTranslation))!;
        Assert.Equal(new[] { "ProductId", "LanguageCode" },
            translations.FindPrimaryKey()!.Properties.Select(x => x.Name));
    }

    [Fact]
    public void UniqueSlugsAndPriceChecksAreMapped()
    {
        using var db = CreateContext();
        foreach (var type in new[] { typeof(PublicCatalogCategory), typeof(PublicCatalogProduct) })
        {
            var entity = db.Model.FindEntityType(type)!;
            Assert.Contains(entity.GetIndexes(), i => i.IsUnique &&
                i.Properties.Select(x => x.Name).SequenceEqual(new[] { "Slug" }));
        }

        var designTimeModel = db.GetService<IDesignTimeModel>().Model;
        var product = designTimeModel.FindEntityType(typeof(PublicCatalogProduct))!;
        Assert.Contains(product.GetCheckConstraints(), c => c.Name == "CK_PublicCatalogProducts_PriceFields");
        Assert.True(product.FindProperty("RowVersion")!.IsConcurrencyToken);
    }
}
