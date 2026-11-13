using Microsoft.EntityFrameworkCore;
using Revestik.Api.Endpoints.PublicCatalog;
using Revestik.Api.Models.PublicProjects;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.PublicProjects;

public sealed class PublicProjectAdminValidationTests
{
    private static PublicProjectAdminEndpoints.ProjectWrite Valid() => new(
        "residential", "Escazú, Costa Rica", null, null,
        ["kitchen", "bathroom"],
        [new("es", "Casa Escazú", "Cocina y baño remodelados.", null, null)], null);

    [Fact]
    public void ProjectRequiresSpanishContentAndKnownType()
    {
        Assert.True(PublicProjectAdminEndpoints.ValidProjectWrite(Valid()));
        Assert.False(PublicProjectAdminEndpoints.ValidProjectWrite(Valid() with { TypeCode = "unknown" }));
        Assert.False(PublicProjectAdminEndpoints.ValidProjectWrite(Valid() with { Translations = [] }));
        Assert.False(PublicProjectAdminEndpoints.ValidProjectWrite(Valid() with
        {
            Translations = [new("en", "English", "English description", null, null)]
        }));
    }

    [Fact]
    public void DuplicateOrUnknownSpacesAndInvalidFeatureSlotsAreRejected()
    {
        Assert.False(PublicProjectAdminEndpoints.ValidProjectWrite(Valid() with
        {
            SpaceCodes = ["kitchen", "kitchen"]
        }));
        Assert.False(PublicProjectAdminEndpoints.ValidProjectWrite(Valid() with
        {
            SpaceCodes = ["unknown"]
        }));
        Assert.False(PublicProjectAdminEndpoints.ValidProjectWrite(Valid() with
        {
            FeaturedOrder = 4
        }));
        Assert.True(PublicProjectAdminEndpoints.ValidProjectWrite(Valid() with
        {
            SpaceCodes = [] // Spaces are optional in the approved portfolio rules.
        }));
    }

    [Fact]
    public void SeoTextAndLocationHaveBoundedLengths()
    {
        Assert.False(PublicProjectAdminEndpoints.ValidProjectWrite(Valid() with
        {
            Location = new string('x', 241)
        }));
        Assert.False(PublicProjectAdminEndpoints.ValidProjectWrite(Valid() with
        {
            Translations = [new("es", "Nombre", "Descripción", new string('x', 181), null)]
        }));
    }
}

// The slug uniqueness check runs against the same disposable Testcontainers database
// used by the existing catalogue and public-project SQL tests.
public sealed class PublicProjectAdminSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    [Fact]
    public async Task GeneratesSlugFromSpanishNameAndHandlesCollisions()
    {
        await using var db = fixture.CreateDbContext();
        var proposed = await PublicProjectAdminEndpoints.GenerateUniqueSlugAsync("Hotel Escazú", db, default);
        Assert.Equal("hotel-escazu", proposed);
        db.Set<PublicProject>().Add(new PublicProject
        {
            Slug = proposed, TypeCode = "hospitality", CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var next = await PublicProjectAdminEndpoints.GenerateUniqueSlugAsync("Hotel Escazú", db, default);
        Assert.Equal("hotel-escazu-2", next);
    }

    [Fact]
    public async Task FeaturedPositionsRemainUniqueInSql()
    {
        await using var db = fixture.CreateDbContext();
        var prefix = Guid.NewGuid().ToString("N");
        db.Set<PublicProject>().AddRange(
            new PublicProject
            {
                Slug = "first-" + prefix, TypeCode = "commercial", FeaturedOrder = 1,
                CreatedAtUtc = DateTime.UtcNow
            },
            new PublicProject
            {
                Slug = "second-" + prefix, TypeCode = "commercial", FeaturedOrder = 1,
                CreatedAtUtc = DateTime.UtcNow
            });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
