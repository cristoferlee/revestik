using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Users;

namespace Revestik.Api.Tests.Authentication;

public sealed class PublicCustomerAdministrationEndpointTests(
    DevelopmentHttpHostFixture fixture)
    : IClassFixture<DevelopmentHttpHostFixture>
{
    private const string ListPath =
        "/api/settings/public-customers";

    private const string StatusPath =
        "/api/settings/public-customers/test-user/status";

    private HttpClient CreateClient(
        string? user = null,
        string? role = null)
    {
        var client = fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = true
            });

        if (user is not null)
            client.DefaultRequestHeaders.Add(
                TestAuthenticationHandler.UserHeaderName,
                user);

        if (role is not null)
            client.DefaultRequestHeaders.Add(
                TestAuthenticationHandler.RoleHeaderName,
                role);

        return client;
    }

    [Theory]
    [InlineData("GET", ListPath)]
    [InlineData("PUT", StatusPath)]
    public async Task AnonymousAccess_IsRejected(
        string method,
        string path)
    {
        using var client = CreateClient();

        using var request =
            new HttpRequestMessage(new HttpMethod(method), path);

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Theory]
    [InlineData("PublicCustomer")]
    [InlineData("Accountant")]
    [InlineData("Sales")]
    [InlineData("Warehouse")]
    public async Task NonAdministrator_CannotReadOrWrite(
        string role)
    {
        using var client = CreateClient(
            "customer-boundary-test", role);

        using var read = await client.GetAsync(ListPath);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            read.StatusCode);

        using var write = await client.PutAsJsonAsync(
            StatusPath,
            new UpdatePublicCustomerStatusRequest
            {
                IsActive = false,
                Version = "test-version"
            });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            write.StatusCode);
    }

    [Fact]
    public async Task Administrator_WithoutCsrf_CannotChangeStatus()
    {
        using var client = CreateClient(
            "customer-administrator-test",
            "Administrator");

        using var response = await client.PutAsJsonAsync(
            StatusPath,
            new UpdatePublicCustomerStatusRequest
            {
                IsActive = false,
                Version = "test-version"
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }
}
