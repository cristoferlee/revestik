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

public sealed class ProjectMilestoneAdditionalHttpTests(SqlServerIntegrationTestFixture fixture)
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


    private static CreateProjectMilestoneTemplateRequest Template() =>
        new("Plantilla " + Guid.NewGuid().ToString("N"), null,
            [new("Medición", null, 30m, 0), new("Instalación", null, 70m, 1)]);

    private static async Task<int> CreateTemplateAsync(HttpClient client, string csrf)
    {
        using var created = await SendAsync(client, HttpMethod.Post,
            "/api/project-milestone-templates", Template(), Owner, "Administrator", csrf);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var data = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        return data.RootElement.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task DeactivatedTemplate_IsNotApplicable_AndExistingProjectRemainsUnchanged()
    {
        var existingId = await SeedAsync();
        var freshId = await SeedAsync();
        using var client = CreateClient();
        var csrf = await TokenAsync(client, Owner, "Administrator");
        var templateId = await CreateTemplateAsync(client, csrf);
        var existingPath = $"/api/projects/{existingId}/milestones";

        using (var applied = await SendAsync(client, HttpMethod.Post, existingPath + "/apply-template",
            new ApplyProjectMilestoneTemplateRequest(templateId, null), Owner, "Administrator", csrf))
            Assert.Equal(HttpStatusCode.OK, applied.StatusCode);

        using (var deleted = await SendAsync(client, HttpMethod.Delete,
            $"/api/project-milestone-templates/{templateId}", new { }, Owner, "Administrator", csrf))
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using (var rejected = await SendAsync(client, HttpMethod.Post,
            $"/api/projects/{freshId}/milestones/apply-template",
            new ApplyProjectMilestoneTemplateRequest(templateId, null), Owner, "Administrator", csrf))
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(2, await db.Set<ProjectMilestone>().CountAsync(x => x.ProjectId == existingId && !x.IsExcluded));
        Assert.False(await db.Set<ProjectMilestonePlan>().AnyAsync(x => x.ProjectId == freshId));
        Assert.False(await db.Set<ProjectMilestoneTemplate>().Where(x => x.Id == templateId)
            .Select(x => x.IsActive).SingleAsync());
    }

    [Fact]
    public async Task ApplyingSameTemplateToTwoProjects_CreatesIndependentMilestones()
    {
        var firstId = await SeedAsync();
        var secondId = await SeedAsync();
        using var client = CreateClient();
        var csrf = await TokenAsync(client, Owner, "Administrator");
        var templateId = await CreateTemplateAsync(client, csrf);
        foreach (var id in new[] { firstId, secondId })
        {
            using var applied = await SendAsync(client, HttpMethod.Post,
                $"/api/projects/{id}/milestones/apply-template",
                new ApplyProjectMilestoneTemplateRequest(templateId, null), Owner, "Administrator", csrf);
            Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        }

        await using var db = fixture.CreateDbContext();
        var firstIds = await db.Set<ProjectMilestone>().AsNoTracking()
            .Where(x => x.ProjectId == firstId).Select(x => x.Id).ToListAsync();
        var secondIds = await db.Set<ProjectMilestone>().AsNoTracking()
            .Where(x => x.ProjectId == secondId).Select(x => x.Id).ToListAsync();
        Assert.Equal(2, firstIds.Count);
        Assert.Equal(2, secondIds.Count);
        Assert.Empty(firstIds.Intersect(secondIds));
    }

    [Fact]
    public async Task DeactivatingTemplate_RequiresAdministratorAndCsrf()
    {
        using var client = CreateClient();
        var adminToken = await TokenAsync(client, Owner, "Administrator");
        var templateId = await CreateTemplateAsync(client, adminToken);
        var endpoint = $"/api/project-milestone-templates/{templateId}";
        var ownerToken = await TokenAsync(client, Owner, "Sales");

        using (var forbidden = await SendAsync(client, HttpMethod.Delete, endpoint,
            new { }, Owner, "Sales", ownerToken))
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using (var csrfMissing = await SendAsync(client, HttpMethod.Delete, endpoint,
            new { }, Owner, "Administrator"))
            Assert.Equal(HttpStatusCode.BadRequest, csrfMissing.StatusCode);

        await using var db = fixture.CreateDbContext();
        Assert.True(await db.Set<ProjectMilestoneTemplate>().Where(x => x.Id == templateId)
            .Select(x => x.IsActive).SingleAsync());
    }

    [Fact]
    public async Task NonOwnerCannotApplyTemplate_EvenWithValidCsrf()
    {
        var projectId = await SeedAsync();
        using var client = CreateClient();
        var adminToken = await TokenAsync(client, Owner, "Administrator");
        var templateId = await CreateTemplateAsync(client, adminToken);
        var outsiderToken = await TokenAsync(client, Outsider, "Accountant");

        using var denied = await SendAsync(client, HttpMethod.Post,
            $"/api/projects/{projectId}/milestones/apply-template",
            new ApplyProjectMilestoneTemplateRequest(templateId, null), Outsider, "Accountant", outsiderToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        await using var db = fixture.CreateDbContext();
        Assert.False(await db.Set<ProjectMilestonePlan>().AnyAsync(x => x.ProjectId == projectId));
    }

    [Fact]
    public async Task ActivationAndCompletion_RequireCsrf_WithoutMutatingPlan()
    {
        var projectId = await SeedAsync();
        using var client = CreateClient();
        var csrf = await TokenAsync(client, Owner, "Sales");
        var path = $"/api/projects/{projectId}/milestones";

        using var configured = await SendAsync(client, HttpMethod.Put, path,
            Configuration(), Owner, "Sales", csrf);
        Assert.Equal(HttpStatusCode.OK, configured.StatusCode);
        using var draft = JsonDocument.Parse(await configured.Content.ReadAsStringAsync());
        var planVersion = draft.RootElement.GetProperty("planVersion").GetInt64();
        var first = draft.RootElement.GetProperty("milestones").EnumerateArray().First();
        var milestoneId = first.GetProperty("id").GetInt32();
        var milestoneVersion = first.GetProperty("version").GetInt64();

        using (var noCsrfActivate = await SendAsync(client, HttpMethod.Post, path + "/activate",
            new ProjectMilestonePlanVersionRequest(planVersion), Owner, "Sales"))
            Assert.Equal(HttpStatusCode.BadRequest, noCsrfActivate.StatusCode);

        using (var activated = await SendAsync(client, HttpMethod.Post, path + "/activate",
            new ProjectMilestonePlanVersionRequest(planVersion), Owner, "Sales", csrf))
            Assert.Equal(HttpStatusCode.OK, activated.StatusCode);

        await using var db = fixture.CreateDbContext();
        var activeVersion = await db.Set<ProjectMilestonePlan>().AsNoTracking()
            .Where(x => x.ProjectId == projectId).Select(x => x.Version).SingleAsync();
        using (var noCsrfCompletion = await SendAsync(client, HttpMethod.Put,
            path + $"/{milestoneId}/completion",
            new SetProjectMilestoneCompletionRequest(true, activeVersion, milestoneVersion, null),
            Owner, "Sales"))
            Assert.Equal(HttpStatusCode.BadRequest, noCsrfCompletion.StatusCode);

        Assert.False(await db.Set<ProjectMilestone>().AsNoTracking()
            .Where(x => x.Id == milestoneId).Select(x => x.IsCompleted).SingleAsync());
    }
}
