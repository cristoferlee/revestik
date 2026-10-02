using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Revestik.Api.Authorization;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.Purchases;

public sealed class PurchaseAuthorizationTests : IAsyncLifetime
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
    [InlineData("/api/purchases?page=1&pageSize=20")]
    [InlineData("/api/purchases/ap-summary")]
    [InlineData("/api/purchases/alerts?ShortWindowDays=3&LongWindowDays=7")]
    [InlineData("/api/purchases/1")]
    [InlineData("/api/purchases/suppliers/1/history?page=1&pageSize=20")]
    public async Task PurchaseReads_WhenAnonymous_ReturnUnauthorized(
        string path)
    {
        using var response =
            await client.GetAsync(path);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Administrator)]
    [InlineData(RoleNames.Accountant)]
    [InlineData(RoleNames.Sales)]
    [InlineData(RoleNames.Warehouse)]
    public async Task PurchaseList_WithApprovedRole_IsAccessible(
        string role)
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/purchases?page=1&pageSize=20");

        request.Headers.Add(
            TestAuthenticationHandler.UserHeaderName,
            "purchase-authorization-user");

        request.Headers.TryAddWithoutValidation(
            TestAuthenticationHandler.RoleHeaderName,
            role);

        using var response =
            await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task PurchaseList_WithUnknownRole_ReturnsForbidden()
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/purchases?page=1&pageSize=20");

        request.Headers.Add(
            TestAuthenticationHandler.UserHeaderName,
            "purchase-authorization-user");

        request.Headers.TryAddWithoutValidation(
            TestAuthenticationHandler.RoleHeaderName,
            "Unassigned");

        using var response =
            await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }
}