using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Revestik.Api.Authorization;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Customers;

namespace Revestik.Api.Tests.Customers;

public sealed class CustomerAuthorizationTests : IAsyncLifetime
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
    [InlineData("GET", "/api/customers")]
    [InlineData("GET", "/api/customers/1")]
    [InlineData("POST", "/api/customers")]
    [InlineData("PUT", "/api/customers/1")]
    [InlineData("DELETE", "/api/customers/1")]
    public async Task CustomerEndpoint_WhenAnonymous_ReturnsUnauthorized(
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
    [InlineData("GET", "/api/customers")]
    [InlineData("GET", "/api/customers/1")]
    [InlineData("POST", "/api/customers")]
    [InlineData("PUT", "/api/customers/1")]
    [InlineData("DELETE", "/api/customers/1")]
    public async Task CustomerEndpoint_WithWarehouseRole_ReturnsForbidden(
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
    public async Task CustomerEndpoint_WithoutAllowedRole_ReturnsForbidden()
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/customers?page=1&pageSize=20",
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
    public async Task GetCustomers_WithAllowedRole_ReturnsOk(string role)
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/customers?page=1&pageSize=20",
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
            "customer-authorization-test-user");
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
                CreateValidCustomerRequest());
        }

        return request;
    }

    private static CustomerUpsertRequest CreateValidCustomerRequest()
    {
        return new CustomerUpsertRequest
        {
            Name = "Authorization Test Customer",
            IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = "123456789",
            Email = "authorization.test@example.com",
            PhoneNumber = "88888888",
            ProvinceCode = "4",
            CantonCode = "01",
            DistrictCode = "01",
            OtherSigns = "Authorization test address",
            IsActive = true
        };
    }
}
