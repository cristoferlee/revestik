using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Users;

namespace Revestik.Api.Tests.Authentication;

public sealed class UserAdministrationEndpointSecurityTests(DevelopmentHttpHostFixture hostFixture)
    : IClassFixture<DevelopmentHttpHostFixture>
{
    private HttpClient CreateClient(string? user = null, string? role = null)
    {
        var client = hostFixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        if (user is not null)
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, user);
        if (role is not null)
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, role);
        return client;
    }

    [Theory]
    [InlineData("GET", "/api/settings/users")]
    [InlineData("GET", "/api/settings/users/test-user")]
    [InlineData("POST", "/api/settings/users")]
    [InlineData("PUT", "/api/settings/users/test-user")]
    public async Task Anonymous_UsersCannotAccessAdministration(string method, string path)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Accountant")]
    [InlineData("Sales")]
    [InlineData("Warehouse")]
    public async Task NonAdministrator_CannotReadUsers(string role)
    {
        using var client = CreateClient("test-nonadministrator", role);
        using var response = await client.GetAsync("/api/settings/users");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/settings/users")]
    [InlineData("PUT", "/api/settings/users/test-user")]
    public async Task NonAdministrator_CannotMutateUsers(string method, string path)
    {
        using var client = CreateClient("test-sales", "Sales");
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new CreateManagedUserRequest
            {
                Email = "new-user@example.com", DisplayName = "New User", Role = "Sales"
            })
        };
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/settings/users")]
    [InlineData("PUT", "/api/settings/users/test-user")]
    public async Task Administrator_WithoutCsrfToken_CannotMutateUsers(string method, string path)
    {
        using var client = CreateClient("test-administrator");
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new CreateManagedUserRequest
            {
                Email = "new-user@example.com", DisplayName = "New User", Role = "Sales"
            })
        };
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
