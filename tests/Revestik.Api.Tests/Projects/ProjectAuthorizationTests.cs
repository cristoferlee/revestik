using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Revestik.Api.Authorization;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Projects;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectAuthorizationTests : IAsyncLifetime
{
    private readonly RevestikWebApplicationFactory factory = new("Development");
    private HttpClient client = null!;

    public Task InitializeAsync()
    {
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Theory]
    [InlineData("GET", "/api/projects?customerId=1")]
    [InlineData("GET", "/api/projects/1")]
    [InlineData("POST", "/api/projects?customerId=1")]
    [InlineData("PUT", "/api/projects/1")]
    [InlineData("PUT", "/api/projects/1/responsible")]
    [InlineData("DELETE", "/api/projects/1?version=1")]
    public async Task AnonymousRequests_AreUnauthorized(string method, string path)
    {
        using var response = await client.SendAsync(CreateRequest(method, path));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/projects?customerId=1")]
    [InlineData("POST", "/api/projects?customerId=1")]
    [InlineData("PUT", "/api/projects/1")]
    [InlineData("DELETE", "/api/projects/1?version=1")]
    public async Task WarehouseRole_IsForbidden(string method, string path)
    {
        using var request = CreateRequest(method, path, RoleNames.Warehouse);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Accountant)]
    [InlineData(RoleNames.Sales)]
    public async Task NonAdmin_CannotReassign(string role)
    {
        using var request = CreateRequest("PUT", "/api/projects/1/responsible", role);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/projects?customerId=1")]
    [InlineData("PUT", "/api/projects/1")]
    [InlineData("PUT", "/api/projects/1/responsible")]
    [InlineData("DELETE", "/api/projects/1?version=1")]
    public async Task AdminMutation_WithoutCsrfToken_IsRejected(string method, string path)
    {
        using var request = CreateRequest(method, path, RoleNames.Administrator);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static HttpRequestMessage CreateRequest(string method, string path, string? role = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (role is not null)
        {
            request.Headers.Add(TestAuthenticationHandler.UserHeaderName, "projects-http-test-user");
            request.Headers.TryAddWithoutValidation(TestAuthenticationHandler.RoleHeaderName, role);
        }
        if (method == "POST" || (method == "PUT" && !path.EndsWith("/responsible", StringComparison.Ordinal)))
            request.Content = JsonContent.Create(new ProjectUpsertRequest(
                "Casa de prueba", null, null, null, null, null, null, null, null, null, null, 1));
        else if (method == "PUT")
            request.Content = JsonContent.Create(new ProjectReassignRequest("projects-http-test-user", 1));
        return request;
    }
}
