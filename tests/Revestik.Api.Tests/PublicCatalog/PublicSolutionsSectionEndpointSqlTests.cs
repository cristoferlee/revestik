using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Revestik.Api.Services.PublicCatalog;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Authentication;
using Revestik.Shared.PublicSite;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicSolutionsSectionEndpointSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    [Fact]
    public async Task SavePersistsThreeLanguagesAndRejectsStaleConcurrentWrite()
    {
        using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "solutions-sql-admin");

        var csrf = await client.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf");
        Assert.NotNull(csrf);
        Assert.False(string.IsNullOrWhiteSpace(csrf.RequestToken));

        // Prime the anonymous output cache to verify that a later save evicts it.
        using var beforeSave = await client.GetAsync("/api/public/site/solutions-sections");
        Assert.Equal(HttpStatusCode.OK, beforeSave.StatusCode);

        var first = new SolutionsSectionWrite
        {
            Es = SolutionsSectionDefaults.Copy("hero", "es"),
            En = SolutionsSectionDefaults.Copy("hero", "en"),
            De = SolutionsSectionDefaults.Copy("hero", "de")
        };
        first.Es.Title = "Título SQL para Soluciones";

        using var saved = await PutWithToken(client, "hero", first, csrf.RequestToken);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var newVersion = await saved.Content.ReadFromJsonAsync<SolutionsSectionDto>();
        Assert.NotNull(newVersion);
        Assert.False(string.IsNullOrEmpty(newVersion.Version));

        // A second editor still holding the empty initial version cannot overwrite the first.
        using var rejected = await PutWithToken(client, "hero", first, csrf.RequestToken);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);

        var updated = new SolutionsSectionWrite
        {
            Version = newVersion.Version,
            Es = first.Es.Clone(),
            En = first.En!.Clone(),
            De = first.De!.Clone()
        };
        updated.Es.Title = "Título actualizado";
        using var secondSave = await PutWithToken(client, "hero", updated, csrf.RequestToken);
        Assert.Equal(HttpStatusCode.OK, secondSave.StatusCode);
        using var stale = await PutWithToken(client, "hero", updated, csrf.RequestToken);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        using var published = await client.GetAsync("/api/public/site/solutions-sections");
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var all = await published.Content.ReadFromJsonAsync<List<SolutionsSectionDto>>();
        var item = Assert.Single(all!, x => x.Key == "hero");
        Assert.Equal("Título actualizado", item.Es.Title);
        Assert.Equal(first.En.Title, item.En!.Title);
        Assert.Equal(first.De.Description, item.De!.Description);
    }

    [Fact]
    public async Task UnknownKeyIsRejectedWithoutCreatingHomeSection()
    {
        using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), HandleCookies = true
        });
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "solutions-bad-key-admin");
        var csrf = await client.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf");
        Assert.NotNull(csrf);
        using var response = await PutWithToken(client, "solutions", new SolutionsSectionWrite
        {
            Es = SolutionsSectionDefaults.Copy("hero", "es")
        }, csrf.RequestToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OnlyHeroMayReceiveImagesAndInvalidTokenIsRejected()
    {
        using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), HandleCookies = true
        });
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "solutions-media-validation-admin");
        var csrf = await client.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf");
        Assert.NotNull(csrf);

        using var wrongSection = await PutWithToken(client, "approach", new SolutionsSectionWrite
        {
            Es = SolutionsSectionDefaults.Copy("approach", "es"),
            ImageToken = new string('a', 32)
        }, csrf.RequestToken);
        Assert.Equal(HttpStatusCode.BadRequest, wrongSection.StatusCode);

        using var invalidHero = await PutWithToken(client, "hero", new SolutionsSectionWrite
        {
            Es = SolutionsSectionDefaults.Copy("hero", "es"),
            ImageToken = "not-a-valid-token"
        }, csrf.RequestToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidHero.StatusCode);
    }

    [Fact]
    public async Task HeroImageCanBeUploadedReadAndRemovedWithoutChangingHome()
    {
        using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "solutions-media-admin");
        var csrf = await client.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf");
        Assert.NotNull(csrf);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Revestik.Api.Data.RevestikDbContext>();
        var homeBefore = await db.PublicHomeSections.AsNoTracking()
            .Where(x => x.Key == "solutions")
            .Select(x => new { x.ContentEs, x.ImageStorageKey })
            .SingleOrDefaultAsync();

        var existing = await client.GetFromJsonAsync<List<SolutionsSectionDto>>(
            "/api/admin/public-site/solutions-sections");
        var hero = existing?.FirstOrDefault(x => x.Key == "hero") ?? SolutionsSectionDefaults.Create("hero");
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAIAAAAmkwkpAAAAFElEQVR4nGMMzatlgAEmBiSAmwMAP1wBSHiZg2QAAAAASUVORK5CYII=");
        await using var imageStream = new MemoryStream(png);
        var storage = scope.ServiceProvider.GetRequiredService<PublicCatalogMediaStorage>();
        var token = await storage.StageAsync(imageStream, "solutions-hero.png", "ProductCover", CancellationToken.None);
        var save = new SolutionsSectionWrite
        {
            Version = hero.Version,
            Es = hero.Es.Clone(),
            En = hero.En?.Clone(),
            De = hero.De?.Clone(),
            ImageToken = token
        };
        using var uploaded = await PutWithToken(client, "hero", save, csrf.RequestToken);
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        var versioned = await uploaded.Content.ReadFromJsonAsync<SolutionsSectionDto>();
        Assert.NotNull(versioned);
        Assert.True(versioned.HasImage);

        using var served = await client.GetAsync("/api/public/site/solutions-sections/hero/media");
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("image/png", served.Content.Headers.ContentType?.MediaType);
        Assert.Equal(png, await served.Content.ReadAsByteArrayAsync());

        var remove = new SolutionsSectionWrite
        {
            Version = versioned.Version,
            Es = save.Es.Clone(),
            En = save.En?.Clone(),
            De = save.De?.Clone(),
            RemoveImage = true
        };
        using var removed = await PutWithToken(client, "hero", remove, csrf.RequestToken);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        var cleared = await removed.Content.ReadFromJsonAsync<SolutionsSectionDto>();
        Assert.NotNull(cleared);
        Assert.False(cleared.HasImage);
        using var absent = await client.GetAsync("/api/public/site/solutions-sections/hero/media");
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);

        db.ChangeTracker.Clear();
        var homeAfter = await db.PublicHomeSections.AsNoTracking()
            .Where(x => x.Key == "solutions")
            .Select(x => new { x.ContentEs, x.ImageStorageKey })
            .SingleOrDefaultAsync();
        Assert.Equal(homeBefore?.ContentEs, homeAfter?.ContentEs);
        Assert.Equal(homeBefore?.ImageStorageKey, homeAfter?.ImageStorageKey);
    }

    private static async Task<HttpResponseMessage> PutWithToken(
        HttpClient client, string key, SolutionsSectionWrite section, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"/api/admin/public-site/solutions-sections/{key}")
        {
            Content = JsonContent.Create(section)
        };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return await client.SendAsync(request);
    }
}
