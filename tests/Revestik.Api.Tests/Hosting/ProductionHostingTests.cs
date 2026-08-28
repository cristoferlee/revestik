using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Revestik.Api.Tests.Hosting;

public sealed class ProductionHostingTests
    : IAsyncLifetime
{
    private readonly RevestikWebApplicationFactory factory =
        new("Production");

    private HttpClient client = null!;

    public Task InitializeAsync()
    {
        client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/customers")]
    [InlineData("/index.html")]
    public async Task ClientRoutes_ReturnIndexWithoutCaching(
        string path)
    {
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "text/html",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            "no-store",
            response.Headers.CacheControl?.ToString());
        Assert.Contains(
            "no-cache",
            response.Headers.CacheControl?.ToString());
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task UnknownApiRoute_ReturnsNotFoundWithoutHtml(
        string method)
    {
        using var request = new HttpRequestMessage(
            new HttpMethod(method),
            "/api/unknown");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual(
            "text/html",
            response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(
            "<html",
            await response.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StaticAsset_UsesManifestCacheHeaders()
    {
        var routes = factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Where(route =>
                route is not null &&
                route.StartsWith(
                    "css/app.",
                    StringComparison.Ordinal) &&
                route.EndsWith(
                    ".css",
                    StringComparison.Ordinal))
            .Cast<string>();

        foreach (var route in routes)
        {
            using var response = await client.GetAsync(route);
            var cacheControl =
                response.Headers.CacheControl?.ToString();

            if (response.IsSuccessStatusCode &&
                cacheControl?.Contains(
                    "immutable",
                    StringComparison.Ordinal) is true)
            {
                Assert.Contains(
                    "max-age=31536000",
                    cacheControl);
                Assert.NotNull(response.Headers.ETag);
                return;
            }
        }

        Assert.Fail(
            "No fingerprinted client asset was served with immutable caching.");
    }

    [Fact]
    public async Task StaticAssetAlias_RequiresRevalidation()
    {
        using var response = await client.GetAsync(
            "/css/app.css");

        response.EnsureSuccessStatusCode();
        Assert.Equal(
            "no-cache",
            response.Headers.CacheControl?.ToString());
        Assert.NotNull(response.Headers.ETag);
    }
}
