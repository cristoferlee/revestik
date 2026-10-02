using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Revestik.Api.Authorization;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Suppliers;

namespace Revestik.Api.Tests.Suppliers;

public sealed class SupplierAuthorizationTests : IAsyncLifetime
{
    private readonly RevestikWebApplicationFactory factory =
        new("Development");

    private HttpClient client = null!;

    public Task InitializeAsync()
    {
        client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
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
    [InlineData("GET", "/api/suppliers")]
    [InlineData("GET", "/api/suppliers/1")]
    [InlineData("POST", "/api/suppliers")]
    [InlineData("PUT", "/api/suppliers/1")]
    [InlineData("POST", "/api/suppliers/1/deactivate")]
    [InlineData("POST", "/api/suppliers/1/reactivate")]
    public async Task SupplierEndpoint_WhenAnonymous_ReturnsUnauthorized(
        string method,
        string path)
    {
        using var request = CreateRequest(new HttpMethod(method), path);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Administrator)]
    [InlineData(RoleNames.Accountant)]
    [InlineData(RoleNames.Sales)]
    [InlineData(RoleNames.Warehouse)]
    public async Task GetSuppliers_WithApprovedRole_ReturnsOk(string role)
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/suppliers?page=1&pageSize=20",
            role);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSuppliers_WithoutIncludeInactive_ReturnsOk()
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/suppliers?page=1&pageSize=20",
            RoleNames.Administrator);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSuppliers_WithUnknownRole_ReturnsForbidden()
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/suppliers?page=1&pageSize=20",
            "Unassigned");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string path,
        string role)
    {
        var request = CreateRequest(method, path);

        request.Headers.Add(
            TestAuthenticationHandler.UserHeaderName,
            "supplier-authorization-test-user");
        request.Headers.TryAddWithoutValidation(
            TestAuthenticationHandler.RoleHeaderName,
            role);

        return request;
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path)
    {
        var request = new HttpRequestMessage(method, path);

        if (method == HttpMethod.Post || method == HttpMethod.Put)
        {
            request.Content = JsonContent.Create(
                new SupplierUpsertRequest
                {
                    Name = "Authorization Supplier",
                    ContactName = "Contacto",
                    PhoneNumber = "88888888",
                    Email = "authorization.supplier@example.com"
                });
        }

        return request;
    }
}