using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Revestik.Public.Services;

namespace Revestik.Api.Tests.Hosting;

[Collection("Isolated production hosting tests")]
public sealed class PublicSiteHostingTests(ProductionHttpHostFixture fixture)
    : IClassFixture<ProductionHttpHostFixture>
{
    [Theory]
    [InlineData("/es/proyectos")]
    [InlineData("/en/projects")]
    [InlineData("/de/projekte")]
    public async Task PublicSsrRoutes_RenderHtmlWithoutAuthentication(string path)
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("REVESTIK", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PublicRoot_RedirectsToLocalizedSsrPage()
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/en", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task UnknownExampleProject_RetainsNotFoundStatus()
    {
        // The production app uses loopback HTTP; TestServer has no listener on
        // that port. Stub only the public project reader's HTTP transport.
        using var factory = CreateProjectHost(simulateOutage: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync("/es/proyectos/inexistente-prueba-404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnknownExampleProject_PublicApiUnavailable_ReturnsServiceUnavailable()
    {
        using var factory = CreateProjectHost(simulateOutage: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync("/es/proyectos/inexistente-prueba-404");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private static RevestikWebApplicationFactory CreateProjectHost(bool simulateOutage) =>
        new("Production", customizeTestServices: services =>
            services.AddHttpClient<PublicProjectsReader>()
                .ConfigurePrimaryHttpMessageHandler(() =>
                    new ProjectDetailHttpHandler(simulateOutage)));

    private sealed class ProjectDetailHttpHandler(bool simulateOutage) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/api/public/projects/inexistente-prueba-404", request.RequestUri?.AbsolutePath);
            Assert.Equal("?language=es", request.RequestUri?.Query);

            if (simulateOutage)
                throw new HttpRequestException("Simulated public projects API outage.");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    [Fact]
    public async Task PrivateApplicationRoute_IsNotShadowedByPublicSsr()
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync("/administracion/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public sealed class PublicSiteStaticAssetsTests(DevelopmentHttpHostFixture fixture)
    : IClassFixture<DevelopmentHttpHostFixture>
{
    [Fact]
    public async Task PublicStylesheet_UsesExistingRootRelativePath()
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync("/css/public.css");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/css", response.Content.Headers.ContentType?.MediaType);
    }
}
