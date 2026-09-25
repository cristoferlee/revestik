using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Revestik.Api.Authorization;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Products;

namespace Revestik.Api.Tests.Products;

public sealed class InventoryCatalogAuthorizationTests : IAsyncLifetime
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
    [InlineData("GET", "/api/product-categories")]
    [InlineData("GET", "/api/product-categories/1")]
    [InlineData("POST", "/api/product-categories")]
    [InlineData("PUT", "/api/product-categories/1")]
    [InlineData("DELETE", "/api/product-categories/1")]
    [InlineData("GET", "/api/units-of-measure")]
    [InlineData("GET", "/api/units-of-measure/1")]
    [InlineData("POST", "/api/units-of-measure")]
    [InlineData("PUT", "/api/units-of-measure/1")]
    [InlineData("DELETE", "/api/units-of-measure/1")]
    public async Task CatalogEndpoint_WhenAnonymous_ReturnsUnauthorized(
        string method,
        string path)
    {
        using var request = CreateRequest(new HttpMethod(method), path);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/product-categories")]
    [InlineData("/api/units-of-measure")]
    public async Task GetCatalog_WithWarehouseRole_ReturnsOk(string path)
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            path,
            RoleNames.Warehouse);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/product-categories")]
    [InlineData("PUT", "/api/product-categories/1")]
    [InlineData("DELETE", "/api/product-categories/1")]
    [InlineData("POST", "/api/units-of-measure")]
    [InlineData("PUT", "/api/units-of-measure/1")]
    [InlineData("DELETE", "/api/units-of-measure/1")]
    public async Task MutateCatalog_WithWarehouseRole_ReturnsForbidden(
        string method,
        string path)
    {
        using var request = CreateAuthenticatedRequest(
            new HttpMethod(method),
            path,
            RoleNames.Warehouse);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Administrator)]
    [InlineData(RoleNames.Accountant)]
    [InlineData(RoleNames.Sales)]
    public async Task GetCatalog_WithCatalogRole_ReturnsOk(string role)
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/product-categories",
            role);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string path,
        string role)
    {
        var request = CreateRequest(method, path);

        request.Headers.Add(
            TestAuthenticationHandler.UserHeaderName,
            "inventory-catalog-authorization-user");
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
            request.Content = path.Contains(
                    "units-of-measure",
                    StringComparison.OrdinalIgnoreCase)
                ? JsonContent.Create(
                    new UnitOfMeasureUpsertRequest
                    {
                        Name = "Caja",
                        Symbol = "caja"
                    })
                : JsonContent.Create(
                    new ProductCategoryUpsertRequest
                    {
                        Name = "Porcelanatos"
                    });
        }

        return request;
    }
}