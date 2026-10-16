using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Customers;
using Revestik.Shared.Projects;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectLifecycleHttpTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string Owner = "b06c-http-owner";
    private const string Other = "b06c-http-other";

    private async Task<int> SeedAsync()
    {
        await using var db = fixture.CreateDbContext();
        foreach (var id in new[] { Owner, Other })
            if (!await db.Users.AnyAsync(x => x.Id == id))
                db.Users.Add(new ApplicationUser
                {
                    Id = id, UserName = id, NormalizedUserName = id.ToUpperInvariant(),
                    Email = $"{id}@example.com", NormalizedEmail = $"{id}@example.com".ToUpperInvariant(),
                    DisplayName = id, SecurityStamp = Guid.NewGuid().ToString()
                });
        var customer = new Customer
        {
            Name = "B06C HTTP", IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9],
            Email = "b06c-http@example.com", PhoneNumber = "88888888",
            ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
            OtherSigns = "Heredia", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var name = Guid.NewGuid().ToString("N");
        var project = new Project
        {
            CustomerId = customer.Id, ResponsibleUserId = Owner,
            Name = name, NormalizedName = name.ToUpperInvariant()
        };
        db.Set<Project>().Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    private static HttpClient Client(RevestikWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false, HandleCookies = true
        });

    private static void Auth(HttpRequestMessage request, string user, string role)
    {
        request.Headers.Add(TestAuthenticationHandler.UserHeaderName, user);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
    }

    private static async Task<string> CsrfAsync(HttpClient client, string user, string role)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/csrf");
        Auth(request, user, role);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("requestToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> SendAsync<T>(HttpClient client, HttpMethod method,
        string url, T payload, string actor, string role, string? token = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(payload) };
        Auth(request, actor, role);
        if (token is not null) request.Headers.Add("X-CSRF-TOKEN", token);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Lifecycle_RequiresAuthenticationAndRole()
    {
        var id = await SeedAsync();
        await using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var client = Client(factory);
        using (var anonymous = await client.GetAsync($"/api/projects/{id}/lifecycle/history"))
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/projects/{id}/lifecycle/status")
        {
            Content = JsonContent.Create(new ProjectTransitionRequest(ProjectStatus.Active, 1, null))
        };
        Auth(request, Other, "Warehouse");
        using var denied = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Lifecycle_MutationRequiresValidCsrfAndOwner()
    {
        var id = await SeedAsync();
        var url = $"/api/projects/{id}/lifecycle/status";
        await using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var client = Client(factory);
        var change = new ProjectTransitionRequest(ProjectStatus.Active, 1, null);
        using (var missing = await SendAsync(client, HttpMethod.Put, url, change, Owner, "Sales"))
            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using (var wrong = await SendAsync(client, HttpMethod.Put, url, change, Owner, "Sales", "invalid-csrf-token"))
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        var otherToken = await CsrfAsync(client, Other, "Sales");
        using (var nonOwner = await SendAsync(client, HttpMethod.Put, url, change, Other, "Sales", otherToken))
            Assert.Equal(HttpStatusCode.Forbidden, nonOwner.StatusCode);
        var ownerToken = await CsrfAsync(client, Owner, "Sales");
        using (var changed = await SendAsync(client, HttpMethod.Put, url, change, Owner, "Sales", ownerToken))
        {
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            var status = await changed.Content.ReadFromJsonAsync<ProjectLifecycleStatusResponse>();
            Assert.NotNull(status);
            Assert.Equal(ProjectStatus.Active, status.Status);
            Assert.Equal(2, status.Version);
        }
        using var stale = await SendAsync(client, HttpMethod.Put, url,
            new ProjectTransitionRequest(ProjectStatus.Paused, 1, null), Owner, "Sales", ownerToken);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using var verify = fixture.CreateDbContext();
        Assert.Equal(ProjectStatus.Active, (await verify.Set<Project>().SingleAsync(x => x.Id == id)).Status);
        Assert.Single(await verify.Set<ProjectLifecycleChange>().Where(x => x.ProjectId == id).ToListAsync());
    }

    [Fact]
    public async Task Lifecycle_CloseReopenAndAdminOnlyHistory()
    {
        var id = await SeedAsync();
        await using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var client = Client(factory);
        var token = await CsrfAsync(client, Owner, "Sales");
        using (var close = await SendAsync(client, HttpMethod.Post, $"/api/projects/{id}/lifecycle/close",
            new ProjectCloseRequest(new DateOnly(2026, 10, 16), "Obra entregada", null, 1), Owner, "Sales", token))
        {
            Assert.Equal(HttpStatusCode.OK, close.StatusCode);
            var status = await close.Content.ReadFromJsonAsync<ProjectLifecycleStatusResponse>();
            Assert.NotNull(status);
            Assert.Equal(ProjectStatus.Completed, status.Status);
            Assert.Equal(2, status.Version);
        }
        using (var historySales = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{id}/lifecycle/history"))
        {
            Auth(historySales, Owner, "Sales");
            using var forbidden = await client.SendAsync(historySales);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
        using (var historyAdmin = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{id}/lifecycle/history"))
        {
            Auth(historyAdmin, Other, "Administrator");
            using var found = await client.SendAsync(historyAdmin);
            Assert.Equal(HttpStatusCode.OK, found.StatusCode);
            var history = await found.Content.ReadFromJsonAsync<List<ProjectLifecycleHistoryResponse>>();
            Assert.NotNull(history);
            Assert.Single(history);
            Assert.Equal("Close", history[0].Action);
            Assert.Equal("Obra entregada", history[0].ClosingObservations);
        }
        using (var reopen = await SendAsync(client, HttpMethod.Post, $"/api/projects/{id}/lifecycle/reopen",
            new ProjectReopenRequest(ProjectStatus.Active, 2, "Garantía"), Owner, "Sales", token))
        {
            Assert.Equal(HttpStatusCode.OK, reopen.StatusCode);
            var status = await reopen.Content.ReadFromJsonAsync<ProjectLifecycleStatusResponse>();
            Assert.NotNull(status);
            Assert.Equal(ProjectStatus.Active, status.Status);
            Assert.Equal(3, status.Version);
        }
        await using var db = fixture.CreateDbContext();
        Assert.Equal(new[] { "Close", "Reopen" },
            await db.Set<ProjectLifecycleChange>().Where(x => x.ProjectId == id)
                .OrderBy(x => x.PreviousProjectVersion).Select(x => x.Action).ToArrayAsync());
    }

    [Fact]
    public async Task Lifecycle_InvalidTransitionsAndMissingProjectDoNotWrite()
    {
        var id = await SeedAsync();
        await using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var client = Client(factory);
        var token = await CsrfAsync(client, Owner, "Sales");
        using (var bad = await SendAsync(client, HttpMethod.Put, $"/api/projects/{id}/lifecycle/status",
            new ProjectTransitionRequest(ProjectStatus.Completed, 1, null), Owner, "Sales", token))
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        using (var bad = await SendAsync(client, HttpMethod.Post, $"/api/projects/{id}/lifecycle/reopen",
            new ProjectReopenRequest(ProjectStatus.Active, 1, "Garantía"), Owner, "Sales", token))
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        using (var missing = await SendAsync(client, HttpMethod.Put, "/api/projects/2147483647/lifecycle/status",
            new ProjectTransitionRequest(ProjectStatus.Active, 1, null), Owner, "Sales", token))
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.Equal(ProjectStatus.Planning, (await db.Set<Project>().SingleAsync(x => x.Id == id)).Status);
        Assert.False(await db.Set<ProjectLifecycleChange>().AnyAsync(x => x.ProjectId == id));
    }
}
