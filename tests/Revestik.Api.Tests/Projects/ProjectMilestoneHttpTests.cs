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

public sealed class ProjectMilestoneHttpTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string Owner = "b03-http-owner";
    private const string Outsider = "b03-http-outsider";

    private async Task<int> SeedAsync()
    {
        await using var db = fixture.CreateDbContext();
        if (!await db.Users.AnyAsync(x => x.Id == Owner))
            db.Users.Add(new ApplicationUser
            {
                Id = Owner, UserName = Owner, NormalizedUserName = Owner.ToUpperInvariant(),
                Email = "b03-http-owner@example.com", NormalizedEmail = "B03-HTTP-OWNER@EXAMPLE.COM",
                DisplayName = "Owner", SecurityStamp = Guid.NewGuid().ToString()
            });
        var customer = new Customer
        {
            Name = "Cliente hitos HTTP", IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9],
            Email = "b03-http@example.com", PhoneNumber = "88888888",
            ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
            OtherSigns = "Heredia", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var unique = Guid.NewGuid().ToString("N");
        var project = new Project
        {
            CustomerId = customer.Id, ResponsibleUserId = Owner,
            Name = "Proyecto " + unique, NormalizedName = unique.ToUpperInvariant()
        };
        db.Set<Project>().Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    private HttpClient CreateClient() =>
        new RevestikWebApplicationFactory("Development", fixture.ConnectionString)
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true
            });

    private static void Authenticate(HttpRequestMessage request, string userId, string role)
    {
        request.Headers.Add(TestAuthenticationHandler.UserHeaderName, userId);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
    }

    private static async Task<string> TokenAsync(HttpClient client, string userId, string role)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/csrf");
        Authenticate(request, userId, role);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = json.RootElement.GetProperty("requestToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token;
    }

    private static async Task<HttpResponseMessage> SendAsync<T>(HttpClient client, HttpMethod method,
        string path, T payload, string userId, string role, string? token = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(payload) };
        Authenticate(request, userId, role);
        if (token is not null) request.Headers.Add("X-CSRF-TOKEN", token);
        return await client.SendAsync(request);
    }

    private static ConfigureProjectMilestonesRequest Configuration(long? version = null) =>
        new([
            new(null, "Medición", null, 30m, 0),
            new(null, "Instalación", null, 70m, 1)
        ], version);

    [Theory]
    [InlineData(null, null, HttpStatusCode.Unauthorized)]
    [InlineData("warehouse", "Warehouse", HttpStatusCode.Forbidden)]
    public async Task ReadingPlan_RequiresPermittedRole(string? actor, string? role, HttpStatusCode expected)
    {
        var projectId = await SeedAsync();
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/milestones");
        if (actor is not null && role is not null) Authenticate(request, actor, role);
        using var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task OwnerWithoutCsrf_CannotConfigure()
    {
        var projectId = await SeedAsync();
        using var client = CreateClient();
        using var response = await SendAsync(client, HttpMethod.Put,
            $"/api/projects/{projectId}/milestones", Configuration(), Owner, "Sales");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.False(await db.Set<ProjectMilestonePlan>().AnyAsync(x => x.ProjectId == projectId));
    }

    [Fact]
    public async Task NonOwnerWithValidCsrf_CannotConfigure()
    {
        var projectId = await SeedAsync();
        using var client = CreateClient();
        var token = await TokenAsync(client, Outsider, "Accountant");
        using var response = await SendAsync(client, HttpMethod.Put,
            $"/api/projects/{projectId}/milestones", Configuration(), Outsider, "Accountant", token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.False(await db.Set<ProjectMilestonePlan>().AnyAsync(x => x.ProjectId == projectId));
    }

    [Fact]
    public async Task OwnerWithValidCsrf_CanConfigureActivateAndComplete_WithConflictOnStaleVersion()
    {
        var projectId = await SeedAsync();
        using var client = CreateClient();
        var token = await TokenAsync(client, Owner, "Sales");
        var path = $"/api/projects/{projectId}/milestones";
        using var created = await SendAsync(client, HttpMethod.Put, path,
            Configuration(), Owner, "Sales", token);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var draft = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var originalVersion = draft.RootElement.GetProperty("planVersion").GetInt64();
        using var activated = await SendAsync(client, HttpMethod.Post, path + "/activate",
            new ProjectMilestonePlanVersionRequest(originalVersion), Owner, "Sales", token);
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        using var active = JsonDocument.Parse(await activated.Content.ReadAsStringAsync());
        Assert.Equal(0m, active.RootElement.GetProperty("percent").GetDecimal());
        var version = active.RootElement.GetProperty("planVersion").GetInt64();
        var first = active.RootElement.GetProperty("milestones").EnumerateArray()
            .Single(x => x.GetProperty("name").GetString() == "Medición");
        var milestoneId = first.GetProperty("id").GetInt32();
        var milestoneVersion = first.GetProperty("version").GetInt64();
        using var done = await SendAsync(client, HttpMethod.Put, path + $"/{milestoneId}/completion",
            new SetProjectMilestoneCompletionRequest(true, version, milestoneVersion, null),
            Owner, "Sales", token);
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        using var completed = JsonDocument.Parse(await done.Content.ReadAsStringAsync());
        Assert.Equal(30m, completed.RootElement.GetProperty("percent").GetDecimal());
        using var stale = await SendAsync(client, HttpMethod.Put, path + $"/{milestoneId}/completion",
            new SetProjectMilestoneCompletionRequest(false, version, milestoneVersion, "Intento obsoleto"),
            Owner, "Sales", token);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task NonAdministrator_CannotCreateTemplate()
    {
        using var client = CreateClient();
        var token = await TokenAsync(client, Owner, "Sales");
        var template = new CreateProjectMilestoneTemplateRequest("No permitida", null,
            [new("Entrega", null, 100m, 0)]);
        using var response = await SendAsync(client, HttpMethod.Post,
            "/api/project-milestone-templates", template, Owner, "Sales", token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdministratorTemplate_CopiesIntoEmptyProject_AndCannotOverwritePlan()
    {
        var projectId = await SeedAsync();
        using var client = CreateClient();
        var token = await TokenAsync(client, Owner, "Administrator");
        var template = new CreateProjectMilestoneTemplateRequest("Plantilla " + Guid.NewGuid().ToString("N"), null,
            [new("Medición", null, 30m, 0), new("Instalación", null, 70m, 1)]);
        using var created = await SendAsync(client, HttpMethod.Post,
            "/api/project-milestone-templates", template, Owner, "Administrator", token);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var data = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var templateId = data.RootElement.GetProperty("id").GetInt32();
        var path = $"/api/projects/{projectId}/milestones";
        using var applied = await SendAsync(client, HttpMethod.Post, path + "/apply-template",
            new ApplyProjectMilestoneTemplateRequest(templateId, null), Owner, "Administrator", token);
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        using var state = JsonDocument.Parse(await applied.Content.ReadAsStringAsync());
        Assert.Equal(2, state.RootElement.GetProperty("milestones").GetArrayLength());
        using var repeated = await SendAsync(client, HttpMethod.Post, path + "/apply-template",
            new ApplyProjectMilestoneTemplateRequest(templateId, null), Owner, "Administrator", token);
        Assert.Equal(HttpStatusCode.BadRequest, repeated.StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.Equal(2, await db.Set<ProjectMilestone>().CountAsync(x => x.ProjectId == projectId && !x.IsExcluded));
    }
}
