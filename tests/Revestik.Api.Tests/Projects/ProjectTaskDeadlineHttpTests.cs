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

public sealed class ProjectTaskDeadlineHttpTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string Owner = "b06-http-owner";
    private const string Other = "b06-http-other";

    private async Task<(int ProjectId, int EventId)> SeedAsync()
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
            Name = "B06 HTTP", IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9],
            Email = "b06-http@example.com", PhoneNumber = "88888888",
            ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
            OtherSigns = "Heredia", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var projectName = Guid.NewGuid().ToString("N");
        var categoryName = Guid.NewGuid().ToString("N");
        var project = new Project
        {
            CustomerId = customer.Id, ResponsibleUserId = Owner,
            Name = projectName, NormalizedName = projectName.ToUpperInvariant()
        };
        var category = new ProjectEventCategory
        {
            Name = categoryName, NormalizedName = categoryName.ToUpperInvariant()
        };
        db.Set<Project>().Add(project);
        db.Set<ProjectEventCategory>().Add(category);
        await db.SaveChangesAsync();
        var ev = new ProjectEvent
        {
            ProjectId = project.Id, CategoryId = category.Id, Title = "Incidencia",
            Description = "Seguimiento", OccurredOn = new DateOnly(2026, 10, 16),
            CreatedByUserId = Owner, CreatedAtUtc = DateTime.UtcNow
        };
        db.Set<ProjectEvent>().Add(ev);
        await db.SaveChangesAsync();
        return (project.Id, ev.Id);
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

    [Fact]
    public async Task TaskEndpoints_RequireAuthenticationAndProjectsRole()
    {
        var (projectId, _) = await SeedAsync();
        await using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var client = Client(factory);
        using (var anonymous = await client.GetAsync($"/api/projects/{projectId}/tasks"))
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var warehouseRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/tasks");
        Auth(warehouseRequest, Other, "Warehouse");
        using var warehouse = await client.SendAsync(warehouseRequest);
        Assert.Equal(HttpStatusCode.Forbidden, warehouse.StatusCode);
    }

    [Fact]
    public async Task TaskMutation_RequiresCsrfAndOwner_AndPreservesHistory()
    {
        var (projectId, _) = await SeedAsync();
        await using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var client = Client(factory);
        var url = $"/api/projects/{projectId}/tasks";
        var input = new ProjectTaskCreateRequest("Visita", "Coordinar", Other, new DateOnly(2026, 10, 23));
        using (var missingToken = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(input) })
        {
            Auth(missingToken, Owner, "Sales");
            using var denied = await client.SendAsync(missingToken);
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        }
        var otherToken = await CsrfAsync(client, Other, "Sales");
        using (var nonOwner = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(input) })
        {
            Auth(nonOwner, Other, "Sales");
            nonOwner.Headers.Add("X-CSRF-TOKEN", otherToken);
            using var denied = await client.SendAsync(nonOwner);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        var ownerToken = await CsrfAsync(client, Owner, "Sales");
        ProjectTaskResponse created;
        using (var create = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(input) })
        {
            Auth(create, Owner, "Sales");
            create.Headers.Add("X-CSRF-TOKEN", ownerToken);
            using var response = await client.SendAsync(create);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            created = (await response.Content.ReadFromJsonAsync<ProjectTaskResponse>())!;
        }
        var updated = new ProjectTaskUpdateRequest("Visita realizada", "Terminado", Other,
            new DateOnly(2026, 10, 24), true, created.Version, "Completada");
        using (var change = new HttpRequestMessage(HttpMethod.Put, $"{url}/{created.Id}") { Content = JsonContent.Create(updated) })
        {
            Auth(change, Owner, "Sales");
            change.Headers.Add("X-CSRF-TOKEN", ownerToken);
            using var response = await client.SendAsync(change);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ProjectTaskResponse>();
            Assert.True(result!.IsCompleted);
        }
        using (var stale = new HttpRequestMessage(HttpMethod.Put, $"{url}/{created.Id}") { Content = JsonContent.Create(updated) })
        {
            Auth(stale, Owner, "Sales");
            stale.Headers.Add("X-CSRF-TOKEN", ownerToken);
            using var response = await client.SendAsync(stale);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        await using var verify = fixture.CreateDbContext();
        Assert.Single(await verify.Set<ProjectTaskRevision>().Where(x => x.ProjectTaskId == created.Id).ToListAsync());
    }

    [Fact]
    public async Task EventDeadline_RequiresOwnerAndCsrf_AndRejectsStaleVersion()
    {
        var (projectId, eventId) = await SeedAsync();
        await using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var client = Client(factory);
        var url = $"/api/projects/{projectId}/events/{eventId}/deadline";
        var first = new ProjectEventDeadlineSetRequest(new DateOnly(2026, 10, 23), null, "Creado");
        var ownerToken = await CsrfAsync(client, Owner, "Sales");
        ProjectEventDeadlineResponse created;
        using (var create = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(first) })
        {
            Auth(create, Owner, "Sales");
            create.Headers.Add("X-CSRF-TOKEN", ownerToken);
            using var response = await client.SendAsync(create);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            created = (await response.Content.ReadFromJsonAsync<ProjectEventDeadlineResponse>())!;
        }
        var second = new ProjectEventDeadlineSetRequest(new DateOnly(2026, 10, 24), created.Version, "Ajuste");
        using (var update = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(second) })
        {
            Auth(update, Owner, "Sales");
            update.Headers.Add("X-CSRF-TOKEN", ownerToken);
            using var response = await client.SendAsync(update);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using (var stale = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(second) })
        {
            Auth(stale, Owner, "Sales");
            stale.Headers.Add("X-CSRF-TOKEN", ownerToken);
            using var response = await client.SendAsync(stale);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        var otherToken = await CsrfAsync(client, Other, "Sales");
        using (var nonOwner = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(first) })
        {
            Auth(nonOwner, Other, "Sales");
            nonOwner.Headers.Add("X-CSRF-TOKEN", otherToken);
            using var response = await client.SendAsync(nonOwner);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        await using var verify = fixture.CreateDbContext();
        Assert.Single(await verify.Set<ProjectEventDeadlineRevision>()
            .Where(x => x.ProjectEventId == eventId).ToListAsync());
    }

    [Fact]
    public async Task ReadOnlyAuthorizedUser_CannotReadHistory_ButAdminCan()
    {
        var (projectId, _) = await SeedAsync();
        await using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var client = Client(factory);
        var url = $"/api/projects/{projectId}/tasks";
        using (var request = new HttpRequestMessage(HttpMethod.Get, url))
        {
            Auth(request, Other, "Sales");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        var token = await CsrfAsync(client, Owner, "Sales");
        int taskId;
        using (var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new ProjectTaskCreateRequest("Verificar", null, null, null))
        })
        {
            Auth(request, Owner, "Sales");
            request.Headers.Add("X-CSRF-TOKEN", token);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            taskId = (await response.Content.ReadFromJsonAsync<ProjectTaskResponse>())!.Id;
        }
        using (var userRequest = new HttpRequestMessage(HttpMethod.Get, $"{url}/{taskId}/history"))
        {
            Auth(userRequest, Other, "Sales");
            using var response = await client.SendAsync(userRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        using (var adminRequest = new HttpRequestMessage(HttpMethod.Get, $"{url}/{taskId}/history"))
        {
            Auth(adminRequest, Other, "Administrator");
            using var response = await client.SendAsync(adminRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
