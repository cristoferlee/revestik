using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Revestik.Api.Authorization;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.Authentication;

public sealed class InternalEndpointAuthorizationBoundaryTests(
    DevelopmentHttpHostFixture hostFixture)
    : IClassFixture<DevelopmentHttpHostFixture>
{
    private HttpClient Client(string? role = null, bool authenticated = false)
    {
        var client = hostFixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
        if (authenticated)
        {
            client.DefaultRequestHeaders.Add(
                TestAuthenticationHandler.UserHeaderName, "auth-boundary-test");
            client.DefaultRequestHeaders.Add(
                TestAuthenticationHandler.RoleHeaderName, role ?? "");
        }
        return client;
    }

    [Theory]
    [InlineData("/api/dashboard/expense-summary")]
    [InlineData("/api/dashboard/gross-margin-summary")]
    [InlineData("/api/dashboard/operating-result-summary")]
    [InlineData("/api/dashboard/document-attention-summary")]
    [InlineData("/api/dashboard/documented-purchases-summary")]
    [InlineData("/api/dashboard/projects-summary")]
    [InlineData("/api/dashboard/recent-activity")]
    [InlineData("/api/dashboard/activity-trend?year=2026&month=11")]
    [InlineData("/api/dashboard/exchange-rate")]
    [InlineData("/api/cabys/status")]
    [InlineData("/api/cabys/")]
    [InlineData("/api/cabys/1234567890123")]
    [InlineData("/api/locations/provinces")]
    [InlineData("/api/locations/provinces/1/cantons")]
    [InlineData("/api/locations/provinces/1/cantons/01/districts")]
    public async Task PublicCustomer_CannotReadBareAuthorizedInternalEndpoint(
        string path)
    {
        using var client = Client("PublicCustomer", authenticated: true);
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/customers")]
    [InlineData("/api/inventory/summary")]
    [InlineData("/api/sales")]
    [InlineData("/api/quotations")]
    [InlineData("/api/purchases")]
    [InlineData("/api/projects?customerId=1")]
    [InlineData("/api/projects/1/documents/1/content")]
    [InlineData("/api/settings/users")]
    [InlineData("/api/settings/audit")]
    [InlineData("/api/admin/public-catalog/products")]
    [InlineData("/api/admin/public-projects")]
    public async Task PublicCustomer_CannotReadRoleProtectedInternalEndpoint(
        string path)
    {
        using var client = Client("PublicCustomer", authenticated: true);
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/dashboard/gross-margin-summary")]
    [InlineData("/api/settings/users")]
    [InlineData("/api/projects?customerId=1")]
    public async Task Anonymous_CannotReadInternalEndpoint(string path)
    {
        using var client = Client();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/dashboard/activity-trend?year=0&month=1")]
    [InlineData("/api/dashboard/recent-activity?limit=0")]
    [InlineData("/api/cabys/not-a-13-digit-code")]
    public async Task InternalEmployee_ReachesExistingBusinessValidation(string path)
    {
        using var client = Client(RoleNames.Warehouse, authenticated: true);
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FallbackPolicy_DeniesPublicUserOnUnmatchedMutableApiRoute()
    {
        using var publicClient = Client("PublicCustomer", authenticated: true);
        using var publicResponse = await publicClient.PostAsync("/api/unknown", null);
        Assert.Equal(HttpStatusCode.Forbidden, publicResponse.StatusCode);

        using var employeeClient = Client(RoleNames.Sales, authenticated: true);
        using var employeeResponse = await employeeClient.PostAsync("/api/unknown", null);
        // The GET/HEAD-only API fallback makes an unmatched POST return 405.
        Assert.Equal(HttpStatusCode.MethodNotAllowed, employeeResponse.StatusCode);
    }

    [Fact]
    public void DefaultAndFallback_RequireAnInternalRole_ButSharedAuthDoesNot()
    {
        var options = hostFixture.Factory.Services
            .GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        foreach (var policy in new[] { options.DefaultPolicy, options.FallbackPolicy! })
        {
            Assert.Contains(policy.Requirements,
                requirement => requirement is DenyAnonymousAuthorizationRequirement);
            var roles = Assert.Single(
                policy.Requirements.OfType<RolesAuthorizationRequirement>());
            Assert.Equal(RoleNames.All, roles.AllowedRoles);
            Assert.DoesNotContain("PublicCustomer", roles.AllowedRoles);
        }

        var sharedPolicy = Assert.IsType<AuthorizationPolicy>(
            options.GetPolicy(PolicyNames.AuthenticatedUser));
        Assert.Contains(sharedPolicy.Requirements,
            requirement => requirement is DenyAnonymousAuthorizationRequirement);
        Assert.Empty(sharedPolicy.Requirements.OfType<RolesAuthorizationRequirement>());
    }
}
