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

public sealed class ProjectDashboardAlertHttpTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string Owner = "b06-dashboard-http-owner";

    private async Task<int> SeedAsync(ProjectStatus status)
    {
        await using var db = fixture.CreateDbContext();
        if (!await db.Users.AnyAsync(x => x.Id == Owner))
            db.Users.Add(new ApplicationUser
            {
                Id = Owner, UserName = Owner, NormalizedUserName = Owner.ToUpperInvariant(),
                Email = "b06-dashboard-http@example.com", NormalizedEmail = "B06-DASHBOARD-HTTP@EXAMPLE.COM",
                DisplayName = Owner, SecurityStamp = Guid.NewGuid().ToString()
            });
        var customer = new Customer
        {
            Name = "B06 Dashboard HTTP", IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9],
            Email = "b06-dashboard-http@example.com", PhoneNumber = "88888888",
            ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
            OtherSigns = "Heredia", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var name = Guid.NewGuid().ToString("N");
        var project = new Project
        {
            CustomerId = customer.Id, ResponsibleUserId = Owner,
            Name = name, NormalizedName = name.ToUpperInvariant(), Status = status
        };
        db.Set<Project>().Add(project);
        await db.SaveChangesAsync();
        db.Set<ProjectTask>().Add(new ProjectTask
        {
            ProjectId = project.Id, Title = "B06 HTTP vencimiento", Description = "",
            DueOn = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-2),
            CreatedByUserId = Owner, CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return project.Id;
    }

    private static HttpClient Client(RevestikWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false, HandleCookies = true
        });

    private static HttpRequestMessage Request(string? user, string? role)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard/project-alerts");
        if (user is not null) request.Headers.Add(TestAuthenticationHandler.UserHeaderName, user);
        if (role is not null) request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        return request;
    }

    [Fact]
    public async Task DashboardAlerts_RejectsAnonymousAndUnauthorizedRoles()
    {
        await using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var client = Client(factory);
        using (var anonymous = Request(null, null))
        using (var result = await client.SendAsync(anonymous))
            Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        using (var otherRole = Request(Owner, "Warehouse"))
        using (var result = await client.SendAsync(otherRole))
            Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
    }

    [Fact]
    public async Task DashboardAlerts_ReturnsDueActiveTaskButExcludesClosedProject()
    {
        var activeId = await SeedAsync(ProjectStatus.Active);
        var closedId = await SeedAsync(ProjectStatus.Completed);
        await using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var client = Client(factory);
        using var request = Request(Owner, "Sales");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var alerts = await response.Content.ReadFromJsonAsync<List<ProjectDashboardAlertResponse>>();
        Assert.NotNull(alerts);
        Assert.Contains(alerts, x => x.ProjectId == activeId && x.Kind == "Task" && x.IsOverdue);
        Assert.DoesNotContain(alerts, x => x.ProjectId == closedId);
    }

    [Fact]
    public async Task DashboardAlerts_GetDoesNotMutateTasks()
    {
        var projectId = await SeedAsync(ProjectStatus.Active);
        await using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var client = Client(factory);
        using (var request = Request(Owner, "Sales"))
        using (var response = await client.SendAsync(request))
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var verify = fixture.CreateDbContext();
        var task = await verify.Set<ProjectTask>().SingleAsync(x => x.ProjectId == projectId);
        Assert.False(task.IsCompleted);
        Assert.Equal(1, task.Version);
        Assert.Null(task.UpdatedAtUtc);
    }
}
