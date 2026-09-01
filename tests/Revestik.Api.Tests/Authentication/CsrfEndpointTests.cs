using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Authentication;

namespace Revestik.Api.Tests.Authentication;

public sealed class CsrfEndpointTests
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
                BaseAddress = new Uri("https://localhost")
            });

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task GetToken_WhenAnonymous_ReturnsUnauthorized()
    {
        using var response = await client.GetAsync(
            "/api/auth/csrf");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task GetToken_WhenAuthenticated_ReturnsTokenAndCookie()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/auth/csrf");
        request.Headers.Add(
            TestAuthenticationHandler.UserHeaderName,
            "csrf-test-user");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload =
            await response.Content
                .ReadFromJsonAsync<CsrfTokenResponse>();

        Assert.NotNull(payload);
        Assert.False(
            string.IsNullOrWhiteSpace(
                payload.RequestToken));

        Assert.True(
            response.Headers.CacheControl?.NoStore);
        Assert.True(
            response.Headers.CacheControl?.NoCache);

        var csrfCookie = response.Headers
            .GetValues("Set-Cookie")
            .Single(value =>
                value.StartsWith(
                    "__Host-Revestik.Csrf=",
                    StringComparison.Ordinal));

        Assert.Contains(
            "path=/",
            csrfCookie,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "secure",
            csrfCookie,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "httponly",
            csrfCookie,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "samesite=lax",
            csrfCookie,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "domain=",
            csrfCookie,
            StringComparison.OrdinalIgnoreCase);
    }
}