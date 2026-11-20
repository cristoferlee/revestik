using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Authentication;
using Revestik.Shared.PublicSite;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicHomeOutputCacheTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    [Fact]
    public async Task PublicSections_CacheRepeatedReads_AndInvalidateAfterAdminSave()
    {
        const string key = "contact";
        var originalTitle = "Before cache " + Guid.NewGuid().ToString("N");
        var externalTitle = "Changed directly " + Guid.NewGuid().ToString("N");
        var savedTitle = "Saved by editor " + Guid.NewGuid().ToString("N");

        // Seed only the isolated SQL Server test database.
        await using (var db = fixture.CreateDbContext())
        {
            var section = await db.PublicHomeSections.SingleOrDefaultAsync(x => x.Key == key);
            if (section is null)
            {
                section = new PublicHomeSection { Key = key, FeaturedSlugsJson = "[]" };
                db.PublicHomeSections.Add(section);
            }
            section.ContentEs = SerializeCopy(originalTitle);
            section.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        await using var factory = new RevestikWebApplicationFactory(
            "Development", fixture.ConnectionString);
        using var publicClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        var first = await ReadPublicSectionAsync(publicClient, key);
        Assert.Equal(originalTitle, first.Es.Title);

        // Prove a cache hit: an out-of-band database update must not appear
        // before the cache is invalidated or expires.
        await using (var db = fixture.CreateDbContext())
        {
            var section = await db.PublicHomeSections.SingleAsync(x => x.Key == key);
            section.ContentEs = SerializeCopy(externalTitle);
            section.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var repeated = await ReadPublicSectionAsync(publicClient, key);
        Assert.Equal(originalTitle, repeated.Es.Title);

        using var admin = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        admin.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.UserHeaderName, "cache-test-admin");
        admin.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.RoleHeaderName, "Administrator");

        var currentSections = await admin.GetFromJsonAsync<List<HomeSectionDto>>(
            "/api/admin/public-site/home-sections");
        var current = Assert.Single(currentSections!, x => x.Key == key);
        Assert.Equal(externalTitle, current.Es.Title);

        using var csrfResponse = await admin.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var token = await csrfResponse.Content.ReadFromJsonAsync<CsrfTokenResponse>();
        Assert.NotNull(token);

        var write = new HomeSectionWrite
        {
            Version = current.Version,
            Es = new HomeSectionCopy
            {
                Title = savedTitle,
                Description = "Public cache invalidation integration test"
            },
            FeaturedSlugs = []
        };
        using var request = new HttpRequestMessage(
            HttpMethod.Put, $"/api/admin/public-site/home-sections/{key}")
        {
            Content = JsonContent.Create(write)
        };
        request.Headers.Add("X-CSRF-TOKEN", token.RequestToken);
        using var saved = await admin.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        // The successful mutation must evict the tagged cached response.
        var refreshed = await ReadPublicSectionAsync(publicClient, key);
        Assert.Equal(savedTitle, refreshed.Es.Title);
    }

    private static string SerializeCopy(string title) =>
        JsonSerializer.Serialize(new HomeSectionCopy
        {
            Title = title,
            Description = "Public cache regression test"
        });

    private static async Task<HomeSectionDto> ReadPublicSectionAsync(
        HttpClient client, string key)
    {
        using var response = await client.GetAsync("/api/public/site/home-sections");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sections = await response.Content.ReadFromJsonAsync<List<HomeSectionDto>>();
        return Assert.Single(sections!, x => x.Key == key);
    }
}
