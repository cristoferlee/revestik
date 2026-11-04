using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicHomeHeroSqlIntegrationTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    [Fact]
    public async Task MigrationCreatesHomeHeroTable()
    {
        await using var db = fixture.CreateDbContext();
        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, x => x.EndsWith("_AddPublicHomeHeroSlides", StringComparison.Ordinal));
        var table = await db.Database.SqlQueryRaw<string>(
            "SELECT [name] AS [Value] FROM sys.tables WHERE [name] = 'PublicHomeHeroSlides'")
            .SingleAsync();
        Assert.Equal("PublicHomeHeroSlides", table);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task OnlyThreeHeroSlotsAreAllowed(int slot)
    {
        await using var db = fixture.CreateDbContext();
        db.PublicHomeHeroSlides.Add(new PublicHomeHeroSlide
        {
            Slot = slot, HeadingEs = "Título de prueba", DescriptionEs = "Descripción de prueba",
            UpdatedAtUtc = DateTime.UtcNow
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
