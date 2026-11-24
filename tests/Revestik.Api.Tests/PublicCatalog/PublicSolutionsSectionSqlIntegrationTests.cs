using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicSolutionsSectionSqlIntegrationTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    [Fact]
    public async Task MigrationCreatesIndependentSolutionsTable()
    {
        await using var db = fixture.CreateDbContext();
        var migrations = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains("20261123181500_AddPublicSolutionsSections", migrations);
        var table = await db.Database.SqlQueryRaw<string>(
            "SELECT [name] AS [Value] FROM sys.tables WHERE [name] = 'PublicSolutionsSections'")
            .SingleAsync();
        Assert.Equal("PublicSolutionsSections", table);
    }

    [Fact]
    public async Task AdditiveMigrationAddsOnlySolutionsHeroImageFields()
    {
        await using var db = fixture.CreateDbContext();
        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains("20261123201500_AddPublicSolutionsHeroImage", applied);
        var columns = await db.Database.SqlQueryRaw<string>(
            "SELECT [name] AS [Value] FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PublicSolutionsSections') " +
            "AND [name] IN ('ImageStorageKey','ImageContentType')")
            .ToListAsync();
        Assert.Contains("ImageStorageKey", columns);
        Assert.Contains("ImageContentType", columns);
    }

    [Fact]
    public async Task DatabaseRejectsImagesOnOtherSolutionsSections()
    {
        await using var db = fixture.CreateDbContext();
        db.PublicSolutionsSections.Add(new PublicSolutionsSection
        {
            Key = "approach", ContentEs = "{}",
            ImageStorageKey = Guid.NewGuid().ToString("N"),
            ImageContentType = "image/png", UpdatedAtUtc = DateTime.UtcNow
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task DatabaseRejectsUnsupportedHeroMediaType()
    {
        await using var db = fixture.CreateDbContext();
        db.PublicSolutionsSections.Add(new PublicSolutionsSection
        {
            Key = "cta", ContentEs = "{}", ImageContentType = "image/gif",
            UpdatedAtUtc = DateTime.UtcNow
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task DatabaseRejectsUnknownKeys()
    {
        await using var db = fixture.CreateDbContext();
        db.PublicSolutionsSections.Add(new PublicSolutionsSection
        {
            Key = "solutions", ContentEs = "{}", UpdatedAtUtc = DateTime.UtcNow
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task WriteIsPersistedIndependentlyOfHomeSolutions()
    {
        await using var db = fixture.CreateDbContext();
        var home = new PublicHomeSection
        {
            Key = "solutions", ContentEs = "{\"title\":\"HOME stays here\"}", UpdatedAtUtc = DateTime.UtcNow
        };
        db.PublicHomeSections.Add(home);
        db.PublicSolutionsSections.Add(new PublicSolutionsSection
        {
            Key = "hero", ContentEs = "{\"title\":\"Page title\"}",
            ContentEn = "{\"title\":\"Page heading\"}", ContentDe = "{\"title\":\"Seitentitel\"}",
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal("{\"title\":\"HOME stays here\"}",
            (await db.PublicHomeSections.SingleAsync(x => x.Key == "solutions")).ContentEs);
        var page = await db.PublicSolutionsSections.SingleAsync(x => x.Key == "hero");
        Assert.Equal("{\"title\":\"Page title\"}", page.ContentEs);
        Assert.Contains("Seitentitel", page.ContentDe);
        Assert.NotEmpty(page.RowVersion);
    }
}
