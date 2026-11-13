using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Models.PublicProjects;
using Revestik.Api.Services.PublicProjects;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.PublicProjects;

// Runs against the repository's disposable SQL Server Testcontainers database.
public sealed class PublicProjectReadSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static PublicProject Project(string type = "residential", string space = "kitchen", bool published = true)
    {
        var p = new PublicProject
        {
            Slug = Unique("portfolio"), TypeCode = type, IsPublished = published,
            CreatedAtUtc = DateTime.UtcNow
        };
        p.Translations.Add(new PublicProjectTranslation
        {
            LanguageCode = "es", Name = "Proyecto de ejemplo", Description = "Materiales y acabados"
        });
        p.Spaces.Add(new PublicProjectSpace { SpaceCode = space });
        p.Images.Add(new PublicProjectImage
        {
            Role = "ProjectCover", StorageKey = Guid.NewGuid().ToString("N"),
            ContentType = "image/jpeg", Width = 1200, Height = 900,
            FileSizeBytes = 512, DisplayOrder = 0, CreatedAtUtc = DateTime.UtcNow
        });
        return p;
    }

    [Fact]
    public async Task FilteringAndPublicationAreEnforcedInSql()
    {
        await using var db = fixture.CreateDbContext();
        var published = Project("residential", "kitchen");
        var hidden = Project("residential", "kitchen", published: false);
        var noCover = Project("residential", "kitchen");
        noCover.Images.Clear();
        db.Set<PublicProject>().AddRange(published, hidden, noCover);
        await db.SaveChangesAsync();

        var result = await PublicProjectReader.ListAsync(db, "es", "residential", "kitchen", 1, default);
        Assert.Single(result.Items);
        Assert.Equal(published.Slug, result.Items[0].Slug);
        Assert.Equal(1, result.Total);
    }

    [Fact]
    public async Task DetailUsesSpanishFallbackAndNeverReturnsUnpublishedProject()
    {
        await using var db = fixture.CreateDbContext();
        var project = Project("institutional", "lobby");
        project.Images.Single().AltEs = "Recepción institucional";
        db.Set<PublicProject>().Add(project);
        await db.SaveChangesAsync();

        var detail = await PublicProjectReader.GetAsync(db, project.Slug, "en", default);
        Assert.NotNull(detail);
        Assert.Equal("Proyecto de ejemplo", detail.Name);
        Assert.Equal("Recepción institucional", detail.Photos.Single().Alt);

        project.IsPublished = false;
        await db.SaveChangesAsync();
        Assert.Null(await PublicProjectReader.GetAsync(db, project.Slug, "en", default));
    }

    [Fact]
    public async Task DeletedProductSnapshotKeepsSafeUnavailableTag()
    {
        await using var db = fixture.CreateDbContext();
        var project = Project("corporate", "office");
        project.Images.Single().Tags.Add(new PublicProjectImageTag
        {
            AnchorX = .25m, AnchorY = .30m, LabelX = .65m, LabelY = .40m,
            ProductNameSnapshotEs = "Porcelanato histórico", DisplayOrder = 0
        });
        db.Set<PublicProject>().Add(project);
        await db.SaveChangesAsync();

        var detail = await PublicProjectReader.GetAsync(db, project.Slug, "de", default);
        var tag = Assert.Single(Assert.Single(detail!.Photos).Tags);
        Assert.Equal("Porcelanato histórico", tag.Label);
        Assert.False(tag.ProductAvailable);
        Assert.Null(tag.ProductSlug);
    }

    [Fact]
    public async Task VisibleCatalogProductNameIsCurrentAndUnpublishedProductBecomesUnavailable()
    {
        await using var db = fixture.CreateDbContext();
        var category = new ProductCategory
        {
            PublicSlug = Unique("category"), Name = Unique("Pisos"), CreatedAtUtc = DateTime.UtcNow
        };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var sub = new ProductSubcategory
        {
            CategoryId = category.Id, PublicSlug = Unique("subcategory"),
            Name = Unique("Porcelanato"), CreatedAtUtc = DateTime.UtcNow
        };
        db.ProductSubcategories.Add(sub);
        var product = new PublicCatalogProduct
        {
            Slug = Unique("material"), IsPublished = true, CreatedAtUtc = DateTime.UtcNow
        };
        product.Translations.Add(new PublicCatalogProductTranslation
        {
            LanguageCode = "es", Name = "Nombre actual", Description = "Material publicado"
        });
        db.PublicCatalogProducts.Add(product);
        await db.SaveChangesAsync();
        db.PublicCatalogProductSubcategories.Add(new()
        {
            ProductId = product.Id, SubcategoryId = sub.Id
        });
        db.Set<PublicCatalogImage>().Add(new PublicCatalogImage
        {
            ProductId = product.Id, Role = "ProductCover", StorageKey = Guid.NewGuid().ToString("N"),
            ContentType = "image/jpeg", Width = 600, Height = 400,
            FileSizeBytes = 512, DisplayOrder = 0, CreatedAtUtc = DateTime.UtcNow
        });

        var project = Project("hospitality", "pool");
        project.Images.Single().Tags.Add(new PublicProjectImageTag
        {
            ProductId = product.Id, ProductNameSnapshotEs = "Nombre anterior",
            AnchorX = .10m, AnchorY = .10m, LabelX = .25m, LabelY = .25m
        });
        db.Set<PublicProject>().Add(project);
        await db.SaveChangesAsync();

        var live = await PublicProjectReader.GetAsync(db, project.Slug, "es", default);
        var liveTag = Assert.Single(Assert.Single(live!.Photos).Tags);
        Assert.True(liveTag.ProductAvailable);
        Assert.Equal("Nombre actual", liveTag.Label);
        Assert.Equal(product.Slug, liveTag.ProductSlug);

        product.IsPublished = false;
        await db.SaveChangesAsync();
        var unpublished = await PublicProjectReader.GetAsync(db, project.Slug, "es", default);
        var unavailableTag = Assert.Single(Assert.Single(unpublished!.Photos).Tags);
        Assert.False(unavailableTag.ProductAvailable);
        Assert.Equal("Nombre anterior", unavailableTag.Label);
        Assert.Null(unavailableTag.ProductSlug);
    }

    [Fact]
    public async Task ManualFeaturedSelectionOverridesNewestFallback()
    {
        await using var db = fixture.CreateDbContext();
        var selected = Project("commercial", "showroom");
        selected.FeaturedOrder = 1;
        selected.CreatedAtUtc = DateTime.UtcNow.AddMonths(-2);
        var recent = Project("commercial", "showroom");
        db.Set<PublicProject>().AddRange(selected, recent);
        await db.SaveChangesAsync();

        var featured = await PublicProjectReader.FeaturedAsync(db, "es", default);
        Assert.Single(featured);
        Assert.Equal(selected.Slug, featured[0].Slug);
    }

    [Fact]
    public async Task ProjectsUseTwelveItemPagesAndStableOrder()
    {
        await using var db = fixture.CreateDbContext();
        var items = Enumerable.Range(0, 14)
            .Select(_ => Project("renovation", "terrace")).ToArray();
        db.Set<PublicProject>().AddRange(items);
        await db.SaveChangesAsync();

        var first = await PublicProjectReader.ListAsync(db, "es", "renovation", "terrace", 1, default);
        var second = await PublicProjectReader.ListAsync(db, "es", "renovation", "terrace", 2, default);
        Assert.Equal(14, first.Total);
        Assert.Equal(12, first.Items.Count);
        Assert.Equal(2, second.Items.Count);
        Assert.Empty(first.Items.Select(x => x.Slug).Intersect(second.Items.Select(x => x.Slug)));
    }
}
