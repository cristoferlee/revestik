using Microsoft.EntityFrameworkCore;
using Revestik.Api.Endpoints.PublicCatalog;
using Revestik.Api.Models;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Models.PublicProjects;
using Revestik.Api.Services.PublicProjects;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.PublicProjects;

public sealed class PublicProjectTagSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private sealed record Seed(int ProductId, int ImageId, int TagId, string ProjectSlug);

    private async Task<Seed> SeedAsync()
    {
        await using var db = fixture.CreateDbContext();
        var token = Guid.NewGuid().ToString("N");
        var category = new ProductCategory
        {
            Name = "Pisos " + token, PublicSlug = "pisos-" + token,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        var subcategory = new ProductSubcategory
        {
            CategoryId = category.Id, PublicSlug = "porcelanato-" + token,
            Name = "Porcelanato", CreatedAtUtc = DateTime.UtcNow
        };
        db.ProductSubcategories.Add(subcategory);
        var product = new PublicCatalogProduct
        {
            Slug = "material-" + token, IsPublished = true, CreatedAtUtc = DateTime.UtcNow,
            Translations = [new PublicCatalogProductTranslation
            {
                LanguageCode = "es", Name = "Porcelanato original", Description = "Material"
            }]
        };
        db.PublicCatalogProducts.Add(product);
        await db.SaveChangesAsync();
        db.PublicCatalogProductSubcategories.Add(new PublicCatalogProductSubcategory
        {
            ProductId = product.Id, SubcategoryId = subcategory.Id
        });
        db.Set<PublicCatalogImage>().Add(new PublicCatalogImage
        {
            ProductId = product.Id, Role = "ProductCover",
            StorageKey = Guid.NewGuid().ToString("N"), ContentType = "image/jpeg",
            Width = 800, Height = 600, FileSizeBytes = 400, CreatedAtUtc = DateTime.UtcNow
        });
        var project = new PublicProject
        {
            Slug = "proyecto-" + token, TypeCode = "residential",
            IsPublished = true, CreatedAtUtc = DateTime.UtcNow,
            Translations = [new PublicProjectTranslation
            {
                LanguageCode = "es", Name = "Proyecto", Description = "Materiales"
            }],
            Images = [new PublicProjectImage
            {
                Role = "ProjectCover", StorageKey = Guid.NewGuid().ToString("N"),
                ContentType = "image/jpeg", Width = 1200, Height = 900,
                FileSizeBytes = 400, CreatedAtUtc = DateTime.UtcNow,
                Tags = [new PublicProjectImageTag
                {
                    ProductId = product.Id, ProductNameSnapshotEs = "Porcelanato original",
                    AnchorX = .2m, AnchorY = .3m, LabelX = .4m, LabelY = .3m
                }]
            }]
        };
        db.Set<PublicProject>().Add(project);
        await db.SaveChangesAsync();
        var image = project.Images.Single();
        return new Seed(product.Id, image.Id, image.Tags.Single().Id, project.Slug);
    }

    [Fact]
    public async Task PublishedRenameRefreshesTagAndPublicReaderUsesLatestName()
    {
        var seed = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var product = await db.PublicCatalogProducts.Include(p => p.Translations)
            .SingleAsync(p => p.Id == seed.ProductId);
        product.Translations.Single(t => t.LanguageCode == "es").Name = "Porcelanato nuevo";
        await PublicProjectTagSnapshots.RefreshPublishedAsync(db, product, default);
        await db.SaveChangesAsync();
        var detail = await PublicProjectReader.GetAsync(db, seed.ProjectSlug, "es", default);
        var tag = Assert.Single(Assert.Single(detail!.Photos).Tags);
        Assert.True(tag.ProductAvailable);
        Assert.Equal("Porcelanato nuevo", tag.ProductName);
        Assert.Equal("Porcelanato nuevo", (await db.Set<PublicProjectImageTag>()
            .SingleAsync(t => t.Id == seed.TagId)).ProductNameSnapshotEs);
    }

    [Fact]
    public async Task UnpublishedProductDoesNotLeakLaterPrivateRename()
    {
        var seed = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var product = await db.PublicCatalogProducts.Include(p => p.Translations)
            .SingleAsync(p => p.Id == seed.ProductId);
        await PublicProjectTagSnapshots.RefreshPublishedAsync(db, product, default);
        product.IsPublished = false;
        await db.SaveChangesAsync();
        product.Translations.Single(t => t.LanguageCode == "es").Name = "Nombre reservado";
        await PublicProjectTagSnapshots.RefreshPublishedAsync(db, product, default);
        await db.SaveChangesAsync();
        var detail = await PublicProjectReader.GetAsync(db, seed.ProjectSlug, "es", default);
        var tag = Assert.Single(Assert.Single(detail!.Photos).Tags);
        Assert.False(tag.ProductAvailable);
        Assert.Equal("Porcelanato original", tag.ProductName);
    }

    [Fact]
    public async Task DeletingPublishedProductKeepsTagAndLatestPublishedSnapshot()
    {
        var seed = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var product = await db.PublicCatalogProducts.Include(p => p.Translations)
            .SingleAsync(p => p.Id == seed.ProductId);
        product.Translations.Single(t => t.LanguageCode == "es").Name = "Último nombre público";
        await db.SaveChangesAsync();
        // Reload rowversion after the published name update.
        var version = Convert.ToBase64String(product.RowVersion);
        var result = await PublicCatalogEndpoints.DeletePublicProductDataAsync(
            seed.ProductId, version, db, "sql-test", default);
        Assert.Equal(PublicCatalogEndpoints.ProductDeletionStatus.Deleted, result.Status);
        db.ChangeTracker.Clear();
        var tag = await db.Set<PublicProjectImageTag>().AsNoTracking()
            .SingleAsync(t => t.Id == seed.TagId);
        Assert.Null(tag.ProductId);
        Assert.Equal("Último nombre público", tag.ProductNameSnapshotEs);
        var detail = await PublicProjectReader.GetAsync(db, seed.ProjectSlug, "es", default);
        Assert.False(Assert.Single(Assert.Single(detail!.Photos).Tags).ProductAvailable);
    }

    [Fact]
    public async Task DeletingUnpublishedProductRetainsLastVisibleSnapshot()
    {
        var seed = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var product = await db.PublicCatalogProducts.Include(p => p.Translations)
            .SingleAsync(p => p.Id == seed.ProductId);
        product.IsPublished = false;
        product.Translations.Single(t => t.LanguageCode == "es").Name = "No público";
        await db.SaveChangesAsync();
        var result = await PublicCatalogEndpoints.DeletePublicProductDataAsync(
            seed.ProductId, Convert.ToBase64String(product.RowVersion), db, "sql-test", default);
        Assert.Equal(PublicCatalogEndpoints.ProductDeletionStatus.Deleted, result.Status);
        db.ChangeTracker.Clear();
        var tag = await db.Set<PublicProjectImageTag>().AsNoTracking()
            .SingleAsync(t => t.Id == seed.TagId);
        Assert.Null(tag.ProductId);
        Assert.Equal("Porcelanato original", tag.ProductNameSnapshotEs);
    }
    [Fact]
    public async Task TagCapacityRejectsSixthHotspotOnOnePhotograph()
    {
        var seed = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        Assert.True(await PublicProjectTagEndpoints.HasTagCapacityAsync(db, seed.ImageId, default));
        for (var n = 1; n <= 4; n++)
        {
            db.Set<PublicProjectImageTag>().Add(new PublicProjectImageTag
            {
                ImageId = seed.ImageId, ProductId = seed.ProductId,
                ProductNameSnapshotEs = "Porcelanato original",
                AnchorX = .2m, AnchorY = .3m, LabelX = .4m, LabelY = .3m,
                DisplayOrder = n
            });
        }
        await db.SaveChangesAsync();
        Assert.False(await PublicProjectTagEndpoints.HasTagCapacityAsync(db, seed.ImageId, default));
        var tag = await db.Set<PublicProjectImageTag>().SingleAsync(t => t.Id == seed.TagId);
        db.Set<PublicProjectImageTag>().Remove(tag);
        await db.SaveChangesAsync();
        Assert.True(await PublicProjectTagEndpoints.HasTagCapacityAsync(db, seed.ImageId, default));
    }

}
