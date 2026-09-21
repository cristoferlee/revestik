using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Revestik.Api.Authorization;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.Products;

public sealed class ProductAuthorizationTests : IAsyncLifetime
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

    [Fact]
    public async Task GetProducts_WhenAnonymous_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/products");

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task GetProducts_WithWarehouseRole_ReturnsForbidden()
    {
        using var request = CreateAuthenticatedRequest(
            RoleNames.Warehouse);

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Administrator)]
    [InlineData(RoleNames.Accountant)]
    [InlineData(RoleNames.Sales)]
    public async Task GetProducts_WithAllowedRole_ReturnsOk(
        string role)
    {
        using var request = CreateAuthenticatedRequest(role);

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(
        string role)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/products?page=1&pageSize=20");

        request.Headers.Add(
            TestAuthenticationHandler.UserHeaderName,
            "product-authorization-test-user");

        request.Headers.TryAddWithoutValidation(
            TestAuthenticationHandler.RoleHeaderName,
            role);

        return request;
    }
}