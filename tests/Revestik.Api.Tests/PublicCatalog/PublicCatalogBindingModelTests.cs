using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.PublicCatalog;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicCatalogBindingModelTests
{
    [Fact]
    public void NoParallelBindingsOrPublicCategoryTablesRemainInModel()
    {
        using var db = new RevestikDbContext(new DbContextOptionsBuilder<RevestikDbContext>()
            .UseSqlServer("Server=(local);Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
        Assert.DoesNotContain(db.Model.GetEntityTypes(), e => e.GetTableName() == "PublicCategoryBindings");
        Assert.DoesNotContain(db.Model.GetEntityTypes(), e => e.GetTableName() == "PublicSubcategoryBindings");
        Assert.DoesNotContain(db.Model.GetEntityTypes(), e => e.GetTableName() == "PublicCatalogCategories");
        Assert.DoesNotContain(db.Model.GetEntityTypes(), e => e.GetTableName() == "PublicCatalogSubcategories");
        Assert.NotNull(db.Model.FindEntityType(typeof(ProductCategory)));
        Assert.NotNull(db.Model.FindEntityType(typeof(ProductSubcategory)));
    }
}
