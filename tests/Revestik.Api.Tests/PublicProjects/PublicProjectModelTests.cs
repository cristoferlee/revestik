using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Models.PublicProjects;

namespace Revestik.Api.Tests.PublicProjects;

// EF metadata tests build the SQL Server model but do not connect to any database.
public sealed class PublicProjectModelTests
{
    private static RevestikDbContext Db() => new(new DbContextOptionsBuilder<RevestikDbContext>()
        .UseSqlServer("Server=(local);Database=RevestikModelOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);

    [Fact]
    public void PublicPortfolioDoesNotReferenceOperationalProjectsOrInventory()
    {
        using var db = Db();
        var model = db.Model.FindEntityType(typeof(PublicProject))!;
        Assert.NotNull(model);
        Assert.DoesNotContain(model.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(Project));
        Assert.DoesNotContain(model.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(Product));
        Assert.DoesNotContain(model.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(PublicCatalogProduct));
    }

    [Fact]
    public void ProjectHasUniqueSlugFeaturedSlotAndConcurrencyToken()
    {
        using var db = Db();
        var entity = db.Model.FindEntityType(typeof(PublicProject))!;
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(PublicProject.Slug) }));
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(PublicProject.FeaturedOrder) }));
        Assert.True(entity.FindProperty(nameof(PublicProject.RowVersion))!.IsConcurrencyToken);
    }

    [Fact]
    public void TranslationsAndSpacesHaveScopedCompositeKeys()
    {
        using var db = Db();
        var translations = db.Model.FindEntityType(typeof(PublicProjectTranslation))!;
        var spaces = db.Model.FindEntityType(typeof(PublicProjectSpace))!;
        Assert.Equal(new[] { "ProjectId", "LanguageCode" },
            translations.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.Equal(new[] { "ProjectId", "SpaceCode" },
            spaces.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.Contains(translations.GetForeignKeys(), fk => fk.DeleteBehavior == DeleteBehavior.Cascade);
        Assert.Contains(spaces.GetForeignKeys(), fk => fk.DeleteBehavior == DeleteBehavior.Cascade);
    }

    [Fact]
    public void ImageHasOneCoverPerProjectAndTagCoordinatesAreConstrained()
    {
        using var db = Db();
        var image = db.Model.FindEntityType(typeof(PublicProjectImage))!;
        Assert.Contains(image.GetIndexes(), i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "ProjectId", "Role" }) &&
            i.GetFilter() == "[Role] = 'ProjectCover'");
        var tag = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(PublicProjectImageTag))!;
        Assert.Contains(tag.GetCheckConstraints(), c => c.Name == "CK_PublicProjectImageTags_Coordinates");
    }

    [Fact]
    public void ProductReferenceIsOptionalAndItsDeletionKeepsTag()
    {
        using var db = Db();
        var tag = db.Model.FindEntityType(typeof(PublicProjectImageTag))!;
        var productFk = Assert.Single(tag.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(PublicCatalogProduct));
        Assert.Equal(DeleteBehavior.SetNull, productFk.DeleteBehavior);
        Assert.True(tag.FindProperty(nameof(PublicProjectImageTag.ProductId))!.IsNullable);
        Assert.True(tag.FindProperty(nameof(PublicProjectImageTag.RowVersion))!.IsConcurrencyToken);
    }

    [Fact]
    public void FixedClassificationCodesAreSeparateFromInventoryTaxonomy()
    {
        Assert.True(PublicProjectClassification.IsKnownType("hospitality"));
        Assert.True(PublicProjectClassification.IsKnownType("renovation"));
        Assert.False(PublicProjectClassification.IsKnownType("kitchen"));
        Assert.True(PublicProjectClassification.IsKnownSpace("kitchen"));
        Assert.True(PublicProjectClassification.IsKnownSpace("pool"));
        Assert.False(PublicProjectClassification.IsKnownSpace("hospitality"));
    }
}
