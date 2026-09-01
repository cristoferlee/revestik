using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Revestik.Api.Data;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Authentication;
using Revestik.Shared.Customers;

namespace Revestik.Api.Tests.Authentication;

public sealed class MutableEndpointCsrfTests
    : IAsyncLifetime
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

        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.UserHeaderName,
            "csrf-test-user");

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Theory]
    [InlineData("POST", "/api/customers")]
    [InlineData("PUT", "/api/customers/1")]
    [InlineData("DELETE", "/api/customers/1")]
    [InlineData("POST", "/api/auth/logout")]
    public async Task MutableEndpoint_WithoutCsrfToken_ReturnsBadRequest(
        string method,
        string path)
    {
        using var request = CreateRequest(
            new HttpMethod(method),
            path);

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task MutableEndpoint_WithInvalidCsrfToken_ReturnsBadRequest()
    {
        await GetCsrfTokenAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/auth/logout");

        request.Headers.Add(
            "X-CSRF-TOKEN",
            "invalid-token");

        request.Content = JsonContent.Create(
            new LogoutRequest(Confirm: true));

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task MutableEndpoint_WithValidCsrfToken_IsAllowed()
    {
        var requestToken = await GetCsrfTokenAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/auth/logout");

        request.Headers.Add(
            "X-CSRF-TOKEN",
            requestToken);

        request.Content = JsonContent.Create(
            new LogoutRequest(Confirm: true));

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);
    }

    [Fact]
    public async Task MutableEndpoint_WhenAnonymous_ReturnsUnauthorized()
    {
        using var anonymousClient = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true
            });

        using var response =
            await anonymousClient.PostAsJsonAsync(
                "/api/auth/logout",
                new LogoutRequest(Confirm: true));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateCustomer_WithoutCsrfToken_DoesNotWriteToDatabase()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/customers")
        {
            Content = JsonContent.Create(
                CreateValidCustomerRequest())
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<RevestikDbContext>();

        Assert.False(
            await dbContext.Customers.AnyAsync());
    }

    private async Task<string> GetCsrfTokenAsync()
    {
        using var response = await client.GetAsync(
            "/api/auth/csrf");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync<CsrfTokenResponse>();

        Assert.NotNull(payload);
        Assert.False(
            string.IsNullOrWhiteSpace(
                payload.RequestToken));

        return payload.RequestToken;
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path)
    {
        var request = new HttpRequestMessage(
            method,
            path);

        if (path == "/api/auth/logout")
        {
            request.Content = JsonContent.Create(
                new LogoutRequest(Confirm: true));
        }
        else if (method != HttpMethod.Delete)
        {
            request.Content = JsonContent.Create(
                CreateValidCustomerRequest());
        }

        return request;
    }

    private static CustomerUpsertRequest
        CreateValidCustomerRequest()
    {
        return new CustomerUpsertRequest
        {
            Name = "CSRF Test Customer",
            IdentificationType =
                IdentificationType.PhysicalPerson,
            IdentificationNumber = "123456789",
            Email = "csrf.test@example.com",
            PhoneNumber = "88888888",
            ProvinceCode = "4",
            CantonCode = "01",
            DistrictCode = "01",
            OtherSigns = "CSRF integration test address",
            IsActive = true
        };
    }
}