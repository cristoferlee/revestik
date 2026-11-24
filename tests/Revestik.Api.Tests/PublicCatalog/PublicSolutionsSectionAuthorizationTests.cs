using System.Net;
using System.Net.Http.Json;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.PublicSite;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicSolutionsSectionAuthorizationTests
{
    [Fact]
    public async Task AnonymousMayReadSolutionsSections()
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/public/site/solutions-sections");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousMayNotReadOrWriteAdministratorSolutionsSections()
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        using var get = await client.GetAsync("/api/admin/public-site/solutions-sections");
        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        using var put = await client.PutAsJsonAsync("/api/admin/public-site/solutions-sections/hero",
            new SolutionsSectionWrite { Es = SolutionsSectionDefaults.Copy("hero", "es") });
        Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
    }

    [Theory]
    [InlineData("Sales")]
    [InlineData("PublicCustomer")]
    public async Task NonAdministratorMayNotReadOrWrite(string role)
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "solutions-other-role");
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, role);
        using var get = await client.GetAsync("/api/admin/public-site/solutions-sections");
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
        using var put = await client.PutAsJsonAsync("/api/admin/public-site/solutions-sections/hero",
            new SolutionsSectionWrite { Es = SolutionsSectionDefaults.Copy("hero", "es") });
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    [Fact]
    public async Task AdministratorWriteWithInvalidCsrfIsRejected()
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        client.BaseAddress = new Uri("https://localhost");
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "solutions-admin-invalid-token");
        using var request = new HttpRequestMessage(HttpMethod.Put,
            "/api/admin/public-site/solutions-sections/hero")
        {
            Content = JsonContent.Create(new SolutionsSectionWrite
            {
                Es = SolutionsSectionDefaults.Copy("hero", "es")
            })
        };
        request.Headers.Add("X-CSRF-TOKEN", "invalid-token");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PublicHeroMediaIsNotFoundUntilPhotoPublished()
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/public/site/solutions-sections/hero/media");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdministratorHeroMediaRequiresAdministratorRole()
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        using var anonymous = await client.GetAsync("/api/admin/public-site/solutions-sections/hero/media");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "solutions-media-sales");
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, "Sales");
        using var unauthorized = await client.GetAsync("/api/admin/public-site/solutions-sections/hero/media");
        Assert.Equal(HttpStatusCode.Forbidden, unauthorized.StatusCode);
    }

    [Fact]
    public async Task AdministratorWriteWithoutCsrfIsRejected()
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        client.BaseAddress = new Uri("https://localhost");
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "solutions-admin");
        using var response = await client.PutAsJsonAsync("/api/admin/public-site/solutions-sections/hero",
            new SolutionsSectionWrite { Es = SolutionsSectionDefaults.Copy("hero", "es") });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
