using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Customers;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectDocumentEventLinkHttpTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string Owner = "b04-e-link-owner";
    private const string Other = "b04-e-link-other";
    private readonly string storagePath = Path.Combine(Path.GetTempPath(),
        "revestik-b04-e-links-" + Guid.NewGuid().ToString("N"));

    private HttpClient Client() =>
        new RevestikWebApplicationFactory("Development", fixture.ConnectionString, storagePath)
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost"),
                HandleCookies = true
            });

    private static void Login(HttpRequestMessage req, string actor = Owner, string role = "Sales")
    {
        req.Headers.Add(TestAuthenticationHandler.UserHeaderName, actor);
        req.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
    }

    private static async Task<string> Token(HttpClient client, string actor = Owner, string role = "Sales")
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/auth/csrf");
        Login(req, actor, role);
        using var response = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("requestToken").GetString()!;
    }

    private async Task<int> SeedProject()
    {
        await using var db = fixture.CreateDbContext();
        if (!await db.Users.AnyAsync(x => x.Id == Owner))
            db.Users.Add(new ApplicationUser
            {
                Id = Owner, UserName = Owner, NormalizedUserName = Owner.ToUpperInvariant(),
                Email = "b04-e-link-owner@example.com",
                NormalizedEmail = "B04-E-LINK-OWNER@EXAMPLE.COM",
                DisplayName = "Owner", SecurityStamp = Guid.NewGuid().ToString()
            });
        var customer = new Customer
        {
            Name = "Cliente B04 E", IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9],
            Email = "b04e@example.com", PhoneNumber = "88888888",
            ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
            OtherSigns = "Heredia", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var key = Guid.NewGuid().ToString("N");
        var project = new Project
        {
            CustomerId = customer.Id, ResponsibleUserId = Owner,
            Name = "Proyecto " + key, NormalizedName = key.ToUpperInvariant()
        };
        db.Set<Project>().Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    private async Task<int> SeedEvent(int projectId)
    {
        await using var db = fixture.CreateDbContext();
        var key = Guid.NewGuid().ToString("N");
        var category = new ProjectEventCategory { Name = "Categoría " + key,
            NormalizedName = key.ToUpperInvariant(), IsActive = true };
        db.Set<ProjectEventCategory>().Add(category);
        await db.SaveChangesAsync();
        var projectEvent = new ProjectEvent
        {
            ProjectId = projectId, CategoryId = category.Id,
            Title = "Visita de obra", Description = "Fotografía de avance",
            OccurredOn = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedByUserId = Owner, CreatedAtUtc = DateTime.UtcNow
        };
        db.Set<ProjectEvent>().Add(projectEvent);
        await db.SaveChangesAsync();
        return projectEvent.Id;
    }

    private static async Task<int> Upload(HttpClient client, int projectId, string csrf)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Evidencia de avance"), "displayName");
        form.Add(new StringContent("Photo"), "category");
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF");
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        form.Add(part, "file", "evidencia.pdf");
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"/api/projects/{projectId}/documents") { Content = form };
        Login(req);
        req.Headers.Add("X-CSRF-TOKEN", csrf);
        using var response = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task<HttpResponseMessage> Change(HttpClient client, HttpMethod method,
        int projectId, int documentId, int eventId, string? csrf,
        string actor = Owner, string role = "Sales")
    {
        using var req = new HttpRequestMessage(method,
            $"/api/projects/{projectId}/documents/{documentId}/events/{eventId}");
        Login(req, actor, role);
        if (csrf is not null) req.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(req);
    }

    [Fact]
    public async Task LinkAndUnlink_ValidEvent_AreIdempotent_AndKeepDocument()
    {
        var project = await SeedProject();
        var eventId = await SeedEvent(project);
        using var client = Client();
        var csrf = await Token(client);
        var document = await Upload(client, project, csrf);
        using (var first = await Change(client, HttpMethod.Post, project, document, eventId, csrf))
            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        using (var repeated = await Change(client, HttpMethod.Post, project, document, eventId, csrf))
            Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
        await using (var db = fixture.CreateDbContext())
        {
            var link = await db.Set<ProjectDocumentEventLink>().SingleAsync(x =>
                x.ProjectDocumentId == document && x.ProjectEventId == eventId);
            Assert.Equal(project, link.ProjectId);
            Assert.Equal(Owner, link.LinkedByUserId);
        }
        using (var unlinked = await Change(client, HttpMethod.Delete, project, document, eventId, csrf))
            Assert.Equal(HttpStatusCode.NoContent, unlinked.StatusCode);
        using (var repeatedDelete = await Change(client, HttpMethod.Delete, project, document, eventId, csrf))
            Assert.Equal(HttpStatusCode.NoContent, repeatedDelete.StatusCode);
        await using var finalDb = fixture.CreateDbContext();
        Assert.False(await finalDb.Set<ProjectDocumentEventLink>().AnyAsync(x =>
            x.ProjectDocumentId == document));
        Assert.True(await finalDb.Set<ProjectDocument>().AnyAsync(x => x.Id == document && x.DeletedAtUtc == null));
        Assert.True(await finalDb.Set<ProjectDocumentVersion>().AnyAsync(x => x.ProjectDocumentId == document));
    }

    [Fact]
    public async Task LinkToForeignProjectEvent_IsRejectedWithNoCrossProjectLink()
    {
        var first = await SeedProject();
        var second = await SeedProject();
        var foreignEvent = await SeedEvent(second);
        using var client = Client();
        var csrf = await Token(client);
        var document = await Upload(client, first, csrf);
        using var response = await Change(client, HttpMethod.Post, first, document, foreignEvent, csrf);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.False(await db.Set<ProjectDocumentEventLink>().AnyAsync(x =>
            x.ProjectDocumentId == document));
    }

    [Fact]
    public async Task LinkAndUnlink_RejectMissingCsrfAndNonResponsibleUser()
    {
        var project = await SeedProject();
        var eventId = await SeedEvent(project);
        using var client = Client();
        var csrf = await Token(client);
        var document = await Upload(client, project, csrf);
        using (var noCsrf = await Change(client, HttpMethod.Post, project, document, eventId, null))
            Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        using (var notOwner = await Change(client, HttpMethod.Post, project, document, eventId,
                   await Token(client, Other, "Accountant"), Other, "Accountant"))
            Assert.Equal(HttpStatusCode.Forbidden, notOwner.StatusCode);
        using (var linked = await Change(client, HttpMethod.Post, project, document, eventId, csrf))
            Assert.Equal(HttpStatusCode.NoContent, linked.StatusCode);
        using (var noCsrfUnlink = await Change(client, HttpMethod.Delete, project, document, eventId, null))
            Assert.Equal(HttpStatusCode.BadRequest, noCsrfUnlink.StatusCode);
        using (var outsiderUnlink = await Change(client, HttpMethod.Delete, project, document, eventId,
                   await Token(client, Other, "Accountant"), Other, "Accountant"))
            Assert.Equal(HttpStatusCode.Forbidden, outsiderUnlink.StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, await db.Set<ProjectDocumentEventLink>().CountAsync(x =>
            x.ProjectDocumentId == document && x.ProjectEventId == eventId));
    }

    [Fact]
    public async Task VoidedEvent_CannotBeLinked()
    {
        var project = await SeedProject();
        var eventId = await SeedEvent(project);
        await using (var db = fixture.CreateDbContext())
        {
            var item = await db.Set<ProjectEvent>().SingleAsync(x => x.Id == eventId);
            item.IsVoided = true;
            item.VoidReason = "Anulado para prueba";
            await db.SaveChangesAsync();
        }
        using var client = Client();
        var csrf = await Token(client);
        var document = await Upload(client, project, csrf);
        using var response = await Change(client, HttpMethod.Post, project, document, eventId, csrf);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var check = fixture.CreateDbContext();
        Assert.False(await check.Set<ProjectDocumentEventLink>().AnyAsync(x => x.ProjectDocumentId == document));
    }
}
