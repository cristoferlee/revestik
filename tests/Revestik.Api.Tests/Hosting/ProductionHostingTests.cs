using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Revestik.Api.Tests.Hosting;

[Collection("Isolated production hosting tests")]
public sealed class ProductionHostingTests(ProductionHttpHostFixture hostFixture)
    : IAsyncLifetime, IClassFixture<ProductionHttpHostFixture>
{
    private RevestikWebApplicationFactory factory => hostFixture.Factory;

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

    public Task DisposeAsync()
    {
        client.Dispose();
        return Task.CompletedTask;
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
    public async Task ForwardedProtoHeader_DoesNotOverrideHttps_WithoutTrustedProxyConfiguration()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "http");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task ForwardedHostHeader_DoesNotChangeAppHost()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", "attacker.example");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void ExplicitProxyMode_OnlyTrustsConfiguredPeerAndProto()
    {
        var options = new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions();
        var trusted = IPAddress.Parse("::ffff:100.100.0.26");
        Revestik.Api.Configuration.RevestikForwardedHeaders.Configure(options, false, new[] { trusted });

        Assert.Equal(Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Equal(1, options.ForwardLimit);
        Assert.Single(options.KnownProxies);
        Assert.Equal(trusted, options.KnownProxies[0]);
        Assert.Empty(options.KnownIPNetworks);
    }

    [Fact]
    public void DynamicIngressMode_OnlyProcessesProtoWithSingleHop()
    {
        var options = new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions();
        Revestik.Api.Configuration.RevestikForwardedHeaders.Configure(options, true, Array.Empty<IPAddress>());

        Assert.Equal(Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Equal(1, options.ForwardLimit);
        Assert.Empty(options.KnownProxies);
        Assert.Empty(options.KnownIPNetworks);
        // Empty trust lists in this opt-in mode mean ANY direct peer can send
        // the scheme header: not safe unless ingress is the only path to Kestrel.
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

// Production Program reads its configuration before factory app-configuration hooks.
// No other test collections should run while fixture-only environment values exist.
[CollectionDefinition("Isolated production hosting tests", DisableParallelization = true)]
public sealed class IsolatedProductionHostingCollection;
