using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Models.PublicProjects;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.PublicProjects;

// Uses the existing Testcontainers SQL Server fixture. All writes target its
// disposable database, never the development or production connection string.
public sealed class PublicProjectSqlIntegrationTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static PublicProject Project(string? slug = null)
    {
        var project = new PublicProject
        {
            Slug = slug ?? Unique("public-project"),
            TypeCode = "residential",
            IsPublished = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        project.Translations.Add(new PublicProjectTranslation
        {
            LanguageCode = "es", Name = "Proyecto de prueba", Description = "Prueba aislada de persistencia."
        });
        project.Spaces.Add(new PublicProjectSpace { SpaceCode = "kitchen" });
        project.Images.Add(new PublicProjectImage
        {
            Role = "ProjectCover", StorageKey = Guid.NewGuid().ToString("N"),
            ContentType = "image/jpeg", Width = 1200, Height = 800,
            FileSizeBytes = 512, DisplayOrder = 0, CreatedAtUtc = DateTime.UtcNow
        });
        return project;
    }

    private static PublicProjectImageTag Tag(int? productId = null) => new()
    {
        ProductId = productId,
        AnchorX = 0.2500m, AnchorY = 0.3000m,
        LabelX = 0.6000m, LabelY = 0.4000m,
        DisplayOrder = 0, ProductNameSnapshotEs = "Porcelanato histórico"
    };

    [Fact]
    public async Task MigrationCreatesFivePublicTablesInIsolatedSqlServer()
    {
        await using var db = fixture.CreateDbContext();
        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, x => x.EndsWith("_AddPublicProjectsFoundation", StringComparison.Ordinal));

        var tables = await db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*) AS [Value]
            FROM sys.tables
            WHERE [name] IN (
                'PublicProjects', 'PublicProjectTranslations', 'PublicProjectSpaces',
                'PublicProjectImages', 'PublicProjectImageTags')
            """).SingleAsync();
        Assert.Equal(5, tables);
    }

    [Fact]
    public async Task DeletingPublicProjectCascadesToImagesTagsTranslationsAndSpaces()
    {
        await using var db = fixture.CreateDbContext();
        var project = Project();
        project.Images.Single().Tags.Add(Tag());
        db.Set<PublicProject>().Add(project);
        await db.SaveChangesAsync();
        var projectId = project.Id;

        // Delete directly in SQL to verify database FKs rather than EF tracked cascades.
        Assert.Equal(1, await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM dbo.PublicProjects WHERE Id = {projectId}"));

        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.Set<PublicProject>().AnyAsync(p => p.Id == projectId));
        Assert.False(await verify.Set<PublicProjectImage>().AnyAsync(i => i.ProjectId == projectId));
        Assert.False(await verify.Set<PublicProjectTranslation>().AnyAsync(t => t.ProjectId == projectId));
        Assert.False(await verify.Set<PublicProjectSpace>().AnyAsync(s => s.ProjectId == projectId));
        Assert.False(await verify.Set<PublicProjectImageTag>()
            .AnyAsync(t => t.Image.ProjectId == projectId));
    }

    [Fact]
    public async Task DeletingPublicCatalogProductPreservesTagWithSnapshotAndNullProductId()
    {
        await using var db = fixture.CreateDbContext();
        var product = new PublicCatalogProduct { Slug = Unique("public-product"), CreatedAtUtc = DateTime.UtcNow };
        db.Set<PublicCatalogProduct>().Add(product);
        await db.SaveChangesAsync();

        var project = Project();
        var tag = Tag(product.Id);
        project.Images.Single().Tags.Add(tag);
        db.Set<PublicProject>().Add(project);
        await db.SaveChangesAsync();
        var tagId = tag.Id;
        var productId = product.Id;

        Assert.Equal(1, await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM dbo.PublicCatalogProducts WHERE Id = {productId}"));

        await using var verify = fixture.CreateDbContext();
        var persisted = await verify.Set<PublicProjectImageTag>().SingleAsync(t => t.Id == tagId);
        Assert.Null(persisted.ProductId);
        Assert.Equal("Porcelanato histórico", persisted.ProductNameSnapshotEs);
    }

    [Fact]
    public async Task SqlCheckConstraintRejectsOutOfRangeTagCoordinates()
    {
        await using var db = fixture.CreateDbContext();
        var project = Project();
        db.Set<PublicProject>().Add(project);
        await db.SaveChangesAsync();
        var imageId = project.Images.Single().Id;

        await using var invalidDb = fixture.CreateDbContext();
        var tag = Tag();
        tag.ImageId = imageId;
        tag.AnchorX = 1.1000m;
        invalidDb.Set<PublicProjectImageTag>().Add(tag);
        await Assert.ThrowsAsync<DbUpdateException>(() => invalidDb.SaveChangesAsync());
    }

    [Fact]
    public async Task UniqueFilteredIndexRejectsSecondProjectCover()
    {
        await using var db = fixture.CreateDbContext();
        var project = Project();
        db.Set<PublicProject>().Add(project);
        await db.SaveChangesAsync();
        var projectId = project.Id;

        await using var duplicateDb = fixture.CreateDbContext();
        duplicateDb.Set<PublicProjectImage>().Add(new PublicProjectImage
        {
            ProjectId = projectId, Role = "ProjectCover",
            StorageKey = Guid.NewGuid().ToString("N"), ContentType = "image/jpeg",
            Width = 600, Height = 400, FileSizeBytes = 512,
            DisplayOrder = 1, CreatedAtUtc = DateTime.UtcNow
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicateDb.SaveChangesAsync());
    }

    [Fact]
    public async Task UniqueFeaturedOrderRejectsTwoProjectsInSameHomeSlot()
    {
        await using var db = fixture.CreateDbContext();
        var first = Project();
        first.FeaturedOrder = 1;
        db.Set<PublicProject>().Add(first);
        await db.SaveChangesAsync();

        await using var duplicateDb = fixture.CreateDbContext();
        var second = Project();
        second.FeaturedOrder = 1;
        duplicateDb.Set<PublicProject>().Add(second);
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicateDb.SaveChangesAsync());
    }
}
