using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.PublicCatalog;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicCatalogBindingRulesTests
{
    [Fact]
    public void ProductCategoryMediaAndClassificationUseSharedForeignKeys()
    {
        using var db = new RevestikDbContext(new DbContextOptionsBuilder<RevestikDbContext>()
            .UseSqlServer("Server=(local);Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
        var image = db.Model.FindEntityType(typeof(PublicCatalogImage))!;
        Assert.Contains(image.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(ProductCategory));
        var link = db.Model.FindEntityType(typeof(PublicCatalogProductSubcategory))!;
        Assert.Contains(link.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(ProductSubcategory));
    }
}
