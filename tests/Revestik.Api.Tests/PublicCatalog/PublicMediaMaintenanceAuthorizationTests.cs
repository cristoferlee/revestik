using System.Net;
using System.Net.Http.Json;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicMediaMaintenanceAuthorizationTests
{
    private const string Route = "/api/admin/public-media/orphans";

    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task DiscoveryAndDeletionRequireAdministrator(bool authenticated, HttpStatusCode expected)
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        if (authenticated)
        {
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "media-sales");
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, "Sales");
        }
        Assert.Equal(expected, (await client.GetAsync(Route)).StatusCode);
        Assert.Equal(expected, (await client.PostAsJsonAsync(Route + "/delete", new { confirmed = true })).StatusCode);
    }

    [Fact]
    public async Task DeletionRequiresAntiforgery()
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        client.BaseAddress = new Uri("https://localhost");
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "media-admin");
        using var response = await client.PostAsJsonAsync(Route + "/delete",
            new { confirmed = true, tickets = new[] { "invalid" } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
