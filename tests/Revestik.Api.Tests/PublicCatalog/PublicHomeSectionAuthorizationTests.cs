using System.Net;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicHomeSectionAuthorizationTests
{
    [Fact]
    public async Task AnonymousCanReadPublicHomeSections()
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/public/site/home-sections");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousCannotReadAdministrativeSections()
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/admin/public-site/home-sections");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SalesCannotReadAdministrativeSections()
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "home-section-sales");
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, "Sales");
        using var response = await client.GetAsync("/api/admin/public-site/home-sections");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdministratorWriteWithoutCsrfIsRejected()
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        client.BaseAddress = new Uri("https://localhost");
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "home-section-admin");
        using var body = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PutAsync("/api/admin/public-site/home-sections/solutions", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
