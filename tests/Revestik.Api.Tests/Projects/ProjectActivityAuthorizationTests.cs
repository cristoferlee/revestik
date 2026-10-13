using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Revestik.Api.Authorization;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Projects;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectActivityAuthorizationTests : IAsyncLifetime
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
    [InlineData("GET", "/api/project-event-categories")]
    [InlineData("POST", "/api/project-event-categories")]
    [InlineData("DELETE", "/api/project-event-categories/1")]
    [InlineData("GET", "/api/projects/1/events")]
    [InlineData("GET", "/api/projects/1/events/1")]
    [InlineData("POST", "/api/projects/1/events")]
    [InlineData("PUT", "/api/projects/1/events/1")]
    [InlineData("POST", "/api/projects/1/events/1/void")]
    [InlineData("GET", "/api/projects/1/events/1/comments")]
    [InlineData("POST", "/api/projects/1/events/1/comments")]
    [InlineData("GET", "/api/projects/1/events/1/agreement")]
    [InlineData("PUT", "/api/projects/1/events/1/agreement")]
    [InlineData("GET", "/api/projects/1/events/1/history")]
    [InlineData("GET", "/api/projects/1/events/1/agreement/history")]
    public async Task Anonymous_IsUnauthorized(string verb, string path)
    {
        using var response = await SendAsync(verb, path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/project-event-categories")]
    [InlineData("POST", "/api/project-event-categories")]
    [InlineData("GET", "/api/projects/1/events")]
    [InlineData("POST", "/api/projects/1/events")]
    [InlineData("GET", "/api/projects/1/events/1/comments")]
    [InlineData("POST", "/api/projects/1/events/1/comments")]
    [InlineData("GET", "/api/projects/1/events/1/agreement")]
    [InlineData("PUT", "/api/projects/1/events/1/agreement")]
    public async Task Warehouse_IsForbidden(string verb, string path)
    {
        using var response = await SendAsync(verb, path, RoleNames.Warehouse);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Accountant, "POST", "/api/project-event-categories")]
    [InlineData(RoleNames.Sales, "DELETE", "/api/project-event-categories/1")]
    [InlineData(RoleNames.Accountant, "GET", "/api/projects/1/events/1/history")]
    [InlineData(RoleNames.Sales, "GET", "/api/projects/1/events/1/agreement/history")]
    public async Task NonAdministrator_CannotManageCategoriesOrReadAudit(string role, string verb, string path)
    {
        using var response = await SendAsync(verb, path, role);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/project-event-categories")]
    [InlineData("DELETE", "/api/project-event-categories/1")]
    [InlineData("POST", "/api/projects/1/events")]
    [InlineData("PUT", "/api/projects/1/events/1")]
    [InlineData("POST", "/api/projects/1/events/1/void")]
    [InlineData("POST", "/api/projects/1/events/1/comments")]
    [InlineData("PUT", "/api/projects/1/events/1/agreement")]
    public async Task AdministratorMutation_WithoutCsrf_IsRejected(string verb, string path)
    {
        using var response = await SendAsync(verb, path, RoleNames.Administrator);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendAsync(string verb, string path, string? role = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(verb), path);
        if (role is not null)
        {
            request.Headers.Add(TestAuthenticationHandler.UserHeaderName, "project-activity-http-user");
            request.Headers.TryAddWithoutValidation(TestAuthenticationHandler.RoleHeaderName, role);
        }
        if (verb is "POST" or "PUT")
            request.Content = JsonContent.Create(new { name = "Test", title = "Test", text = "Test", reason = "Test" });
        return await client.SendAsync(request);
    }
}
