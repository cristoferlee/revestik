using System.Net;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicHomeHeroAuthorizationTests
{
    [Fact]
    public async Task AnonymousMayReadPublishedHeroMetadata()
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/public/site/home-hero");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdministrativeHeroRequiresAuthentication()
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/admin/public-site/home-hero");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SalesCannotReadHeroAdministration()
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "home-hero-sales");
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, "Sales");
        var response = await client.GetAsync("/api/admin/public-site/home-hero");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task HeroWriteWithoutCsrfIsRejected()
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        client.BaseAddress = new Uri("https://localhost");
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "home-hero-admin");
        using var body = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PutAsync("/api/admin/public-site/home-hero/1", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
