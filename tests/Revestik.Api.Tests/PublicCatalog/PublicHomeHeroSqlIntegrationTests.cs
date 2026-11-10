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

    [Fact]
    public async Task MigrationReservesIndependentSlotsForFinishes()
    {
        await using var db = fixture.CreateDbContext();
        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, x => x.EndsWith("_AllowFinishesHeroSlots", StringComparison.Ordinal));
        var constraint = await db.Database.SqlQueryRaw<string>(
            "SELECT [definition] AS [Value] FROM sys.check_constraints WHERE [name] = 'CK_PublicHomeHeroSlides_Slot'")
            .SingleAsync();
        Assert.Contains("6", constraint);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task SlotsOutsideBothPagesAreRejected(int slot)
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
