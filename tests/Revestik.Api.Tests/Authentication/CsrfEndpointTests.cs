using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Revestik.Api.Models.Identity;
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

    [Fact]
    public async Task GetToken_WhenPublicCustomer_ReturnsToken()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/api/auth/csrf");
        request.Headers.Add(
            TestAuthenticationHandler.UserHeaderName, "public-auth-test");
        request.Headers.Add(
            TestAuthenticationHandler.RoleHeaderName, "PublicCustomer");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content
            .ReadFromJsonAsync<CsrfTokenResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.RequestToken));
    }

    [Fact]
    public async Task GetCurrentUser_WhenPublicCustomerExists_ReturnsIdentity()
    {
        const string publicRole = "PublicCustomer";
        using var scope = factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync(publicRole))
            Assert.True((await roleManager.CreateAsync(new IdentityRole(publicRole))).Succeeded);

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"public-me-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Public identity test",
            IsActive = true
        };
        Assert.True((await userManager.CreateAsync(user)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, publicRole)).Succeeded);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add(TestAuthenticationHandler.UserHeaderName, user.Id);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, publicRole);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(payload);
        Assert.Equal(user.Id, payload.Id);
        Assert.Equal(new[] { publicRole }, payload.Roles);
    }

    [Fact]
    public async Task Logout_WhenPublicCustomer_RequiresValidCsrf()
    {
        using var publicClient = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = true
            });
        publicClient.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.UserHeaderName, "public-logout-test");
        publicClient.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.RoleHeaderName, "PublicCustomer");

        using var csrfResponse = await publicClient.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var token = await csrfResponse.Content.ReadFromJsonAsync<CsrfTokenResponse>();
        Assert.NotNull(token);

        using var withoutToken = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout")
        {
            Content = JsonContent.Create(new LogoutRequest(Confirm: true))
        };
        using var rejected = await publicClient.SendAsync(withoutToken);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        using var withToken = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout")
        {
            Content = JsonContent.Create(new LogoutRequest(Confirm: true))
        };
        withToken.Headers.Add("X-CSRF-TOKEN", token.RequestToken);
        using var accepted = await publicClient.SendAsync(withToken);
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
    }
}