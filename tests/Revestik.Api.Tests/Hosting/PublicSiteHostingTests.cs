using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

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
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync("/es/proyectos/inexistente-prueba-404");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
