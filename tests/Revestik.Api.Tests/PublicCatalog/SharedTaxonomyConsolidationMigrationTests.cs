using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Revestik.Api.Data.Migrations;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class SharedTaxonomyConsolidationMigrationTests
{
    [Fact]
    public void MigrationNeverDropsInventoryOrOperationalProductTables()
    {
        var migration = new ConsolidateSharedProductTaxonomy();
        var dropped = migration.UpOperations.OfType<DropTableOperation>()
            .Select(x => x.Name).ToArray();
        Assert.Equal(4, dropped.Length);
        Assert.Contains("PublicCatalogCategories", dropped);
        Assert.Contains("PublicCatalogSubcategories", dropped);
        Assert.Contains("PublicCategoryBindings", dropped);
        Assert.Contains("PublicSubcategoryBindings", dropped);
        Assert.DoesNotContain("ProductCategories", dropped);
        Assert.DoesNotContain("ProductSubcategories", dropped);
        Assert.DoesNotContain("Products", dropped);
        Assert.DoesNotContain("InventoryCostLayers", dropped);
        Assert.DoesNotContain("PublicCatalogProducts", dropped);
        Assert.DoesNotContain("PublicCatalogImages", dropped);
    }

    [Fact]
    public void MigrationBackfillsSeoProductMembershipAndImagesBeforeDroppingTables()
    {
        var migration = new ConsolidateSharedProductTaxonomy();
        var operations = migration.UpOperations.ToList();
        var sqlIndex = operations.FindIndex(x => x is SqlOperation);
        var dropIndex = operations.FindIndex(x => x is DropTableOperation);
        Assert.True(sqlIndex >= 0 && dropIndex > sqlIndex);
        var sql = Assert.IsType<SqlOperation>(operations[sqlIndex]).Sql;
        Assert.Contains("INSERT INTO dbo.ProductCategories", sql);
        Assert.Contains("INSERT INTO dbo.ProductSubcategories", sql);
        Assert.Contains("UPDATE link SET SubcategoryId", sql);
        Assert.Contains("UPDATE img SET CategoryId", sql);
        Assert.Contains("Manual category bindings found", sql);
        Assert.Contains("Orphaned category image", sql);
    }
}
