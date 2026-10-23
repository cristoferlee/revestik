using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Revestik.Api.Authorization;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Projects;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectPaymentHttpTests(DevelopmentHttpHostFixture hostFixture)
    : IAsyncLifetime, IClassFixture<DevelopmentHttpHostFixture>
{
    private RevestikWebApplicationFactory factory => hostFixture.Factory;
    private HttpClient client = null!;
    public Task InitializeAsync()
    {
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
        });
        return Task.CompletedTask;
    }
    public Task DisposeAsync()
    {
        client.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("GET", "/api/projects/1/payments")]
    [InlineData("GET", "/api/projects/1/payments/summary")]
    [InlineData("GET", "/api/projects/1/payments/1/history")]
    [InlineData("GET", "/api/projects/1/payments/1/documents")]
    [InlineData("POST", "/api/projects/1/payments")]
    [InlineData("PUT", "/api/projects/1/agreed-amount")]
    [InlineData("POST", "/api/projects/1/payments/1/void")]
    [InlineData("POST", "/api/projects/1/payments/1/documents/1")]
    public async Task AnonymousPaymentsEndpoints_RequireAuthentication(string method, string path)
    {
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/projects/1/payments")]
    [InlineData("GET", "/api/projects/1/payments/1/documents")]
    [InlineData("POST", "/api/projects/1/payments")]
    [InlineData("PUT", "/api/projects/1/agreed-amount")]
    public async Task WarehouseRole_IsForbidden(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add(TestAuthenticationHandler.UserHeaderName, "b05bc-http");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, RoleNames.Warehouse);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/projects/1/payments")]
    [InlineData("PUT", "/api/projects/1/payments/1")]
    [InlineData("POST", "/api/projects/1/payments/1/void")]
    [InlineData("PUT", "/api/projects/1/agreed-amount")]
    [InlineData("POST", "/api/projects/1/payments/1/documents/1")]
    [InlineData("DELETE", "/api/projects/1/payments/1/documents/1")]
    public async Task AdminMutationWithoutCsrf_IsRejected(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add(TestAuthenticationHandler.UserHeaderName, "b05bc-http");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, RoleNames.Administrator);
        if (method is "POST" or "PUT") request.Content = JsonContent.Create(new ProjectPaymentUpsertRequest(
            10, "CRC", new DateOnly(2026, 10, 16), null, null));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
