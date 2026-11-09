using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.PublicCatalog;

namespace Revestik.Api.Tests.Products;

public sealed class SharedProductTaxonomyModelTests
{
    private static RevestikDbContext Context() => new(
        new DbContextOptionsBuilder<RevestikDbContext>()
            .UseSqlServer("Server=(local);Database=RevestikTaxonomyModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);

    [Fact]
    public void SharedSubcategoriesBelongToInternalCategoriesAndHaveUniqueNamesWithinCategory()
    {
        using var db = Context();
        var model = db.Model.FindEntityType(typeof(ProductSubcategory))!;
        Assert.Equal("ProductSubcategories", model.GetTableName());
        Assert.Contains(model.GetForeignKeys(), f =>
            f.PrincipalEntityType.ClrType == typeof(ProductCategory) &&
            f.Properties.Select(p => p.Name).SequenceEqual(["CategoryId"]) &&
            f.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(model.GetIndexes(), i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(["CategoryId", "Name"]));
    }

    [Fact]
    public void OperationalProductSubcategoryIsOptionalAndDoesNotReplaceCategory()
    {
        using var db = Context();
        var model = db.Model.FindEntityType(typeof(Product))!;
        Assert.True(model.FindProperty("SubcategoryId")!.IsNullable);
        Assert.False(model.FindProperty("CategoryId")!.IsNullable);
        Assert.Contains(model.GetForeignKeys(), f =>
            f.PrincipalEntityType.ClrType == typeof(ProductSubcategory) &&
            f.DeleteBehavior == DeleteBehavior.Restrict);
    }

    [Fact]
    public void PublicProductsUseSharedTaxonomyAndRemainIndependentOfInventory()
    {
        using var db = Context();
        var model = db.Model;

        // Removed taxonomy and manual-binding types must not be mapped.
        // Assert by metadata name so this test does not depend on obsolete CLR types.
        Assert.DoesNotContain(model.GetEntityTypes(), entity =>
            entity.ClrType.Name is "PublicCategoryBinding" or "PublicSubcategoryBinding"
                or "PublicCatalogCategory" or "PublicCatalogSubcategory");
        Assert.DoesNotContain(model.GetEntityTypes(), entity =>
            entity.GetTableName() is "PublicCategoryBindings" or "PublicSubcategoryBindings"
                or "PublicCatalogCategories" or "PublicCatalogSubcategories");

        var publicProduct = model.FindEntityType(typeof(PublicCatalogProduct))!;
        Assert.DoesNotContain(publicProduct.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(Product));

        var membership = model.FindEntityType(typeof(PublicCatalogProductSubcategory))!;
        Assert.Contains(membership.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(ProductSubcategory) &&
            fk.Properties.Select(p => p.Name).SequenceEqual(["SubcategoryId"]));

        var image = model.FindEntityType(typeof(PublicCatalogImage))!;
        Assert.Contains(image.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(ProductCategory) &&
            fk.Properties.Select(p => p.Name).SequenceEqual(["CategoryId"]));
    }
}
