using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Revestik.Api.Authorization;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Quotations;

namespace Revestik.Api.Tests.Quotations;

public sealed class QuotationAuthorizationTests : IAsyncLifetime
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
    [InlineData("GET", "/api/quotations/1")]
    [InlineData("POST", "/api/quotations")]
    [InlineData("PUT", "/api/quotations/1")]
    public async Task QuotationEndpoint_WhenAnonymous_ReturnsUnauthorized(
        string method,
        string path)
    {
        using var request = CreateRequest(
            new HttpMethod(method),
            path);

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/quotations/1")]
    [InlineData("POST", "/api/quotations")]
    [InlineData("PUT", "/api/quotations/1")]
    public async Task QuotationEndpoint_WithWarehouseRole_ReturnsForbidden(
        string method,
        string path)
    {
        using var request = CreateAuthenticatedRequest(
            new HttpMethod(method),
            path,
            RoleNames.Warehouse);

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task QuotationEndpoint_WithoutAllowedRole_ReturnsForbidden()
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/quotations/1",
            "Unassigned");

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Administrator)]
    [InlineData(RoleNames.Accountant)]
    [InlineData(RoleNames.Sales)]
    public async Task GetQuotation_WithAllowedRole_ReachesEndpoint(
        string role)
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/quotations/1",
            role);

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string path,
        string role)
    {
        var request = CreateRequest(method, path);

        request.Headers.Add(
            TestAuthenticationHandler.UserHeaderName,
            "quotation-authorization-test-user");

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
                CreateValidQuotationRequest());
        }

        return request;
    }

    private static QuotationUpsertRequest CreateValidQuotationRequest()
    {
        return new QuotationUpsertRequest
        {
            CustomerId = 1,
            Lines =
            [
                new QuotationLineRequest
                {
                    CabysCode = "1234567890123",
                    Description = "Authorization test product",
                    Quantity = 1m,
                    UnitPrice = 113m,
                    TaxRate = 13m
                }
            ]
        };
    }
}