using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicHomeSectionSqlIntegrationTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private static PublicHomeSection NewSection(string key) => new()
    {
        Key = key,
        ContentEs = "{\"title\":\"Ejemplo\"}",
        UpdatedAtUtc = DateTime.UtcNow
    };

    [Fact]
    public async Task MigrationCreatesHomeSectionsTable()
    {
        await using var db = fixture.CreateDbContext();
        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, x => x.EndsWith("_AddPublicHomeSections", StringComparison.Ordinal));
        var name = await db.Database.SqlQueryRaw<string>(
            "SELECT [name] AS [Value] FROM sys.tables WHERE [name] = 'PublicHomeSections'").SingleAsync();
        Assert.Equal("PublicHomeSections", name);
    }

    [Fact]
    public async Task InvalidSectionKeyIsRejected()
    {
        await using var db = fixture.CreateDbContext();
        db.PublicHomeSections.Add(NewSection("navigation"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task NonSolutionsImageIsRejected()
    {
        await using var db = fixture.CreateDbContext();
        var content = NewSection("contact");
        content.ImageStorageKey = Guid.NewGuid().ToString("N");
        content.ImageContentType = "image/png";
        db.PublicHomeSections.Add(content);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
