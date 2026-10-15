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

public sealed class ProjectEventOwnershipHttpTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string OwnerId = "b02-http-owner";
    private const string NonOwnerId = "b02-http-nonowner";

    [Theory]
    [InlineData("create-event", "Sales")]
    [InlineData("update-event", "Accountant")]
    [InlineData("void-event", "Sales")]
    [InlineData("add-comment", "Accountant")]
    [InlineData("create-agreement", "Sales")]
    [InlineData("update-agreement", "Accountant")]
    public async Task PermittedRoleWithoutOwnership_WithValidCsrf_CannotMutate(
        string operation, string role)
    {
        var (projectId, categoryId, eventId) = await SeedAsync(
            withAgreement: operation == "update-agreement");
        await using var factory = new RevestikWebApplicationFactory(
            "Development", fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var csrfToken = await GetCsrfAsync(client, NonOwnerId, role);
        var basePath = $"/api/projects/{projectId}/events";
        using var request = operation switch
        {
            "create-event" => CreateRequest(HttpMethod.Post, basePath,
                new ProjectEventUpsertRequest(categoryId, "Intrusión", null, new DateOnly(2026, 10, 1))),
            "update-event" => CreateRequest(HttpMethod.Put, $"{basePath}/{eventId}",
                new ProjectEventUpsertRequest(categoryId, "Alterado", null, new DateOnly(2026, 10, 1), 1)),
            "void-event" => CreateRequest(HttpMethod.Post, $"{basePath}/{eventId}/void",
                new ProjectEventVoidRequest(1, "Sin autorización")),
            "add-comment" => CreateRequest(HttpMethod.Post, $"{basePath}/{eventId}/comments",
                new ProjectCommentCreateRequest("Comentario no autorizado")),
            "create-agreement" => CreateRequest(HttpMethod.Put, $"{basePath}/{eventId}/agreement",
                new ProjectAgreementUpsertRequest(ProjectAgreementStatus.Pending, null, null, null, "Intrusión")),
            "update-agreement" => CreateRequest(HttpMethod.Put, $"{basePath}/{eventId}/agreement",
                new ProjectAgreementUpsertRequest(ProjectAgreementStatus.Fulfilled, null, null, null, "Alterado", 1)),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        Authenticate(request, NonOwnerId, role);
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var verify = fixture.CreateDbContext();
        var events = await verify.Set<ProjectEvent>().AsNoTracking()
            .Where(x => x.ProjectId == projectId).ToListAsync();
        var ev = Assert.Single(events);
        Assert.Equal(eventId, ev.Id);
        Assert.Equal("Visita original", ev.Title);
        Assert.False(ev.IsVoided);
        Assert.Equal(1, ev.Version);
        Assert.Empty(await verify.Set<ProjectEventComment>().AsNoTracking()
            .Where(x => x.ProjectEventId == eventId).ToListAsync());
        Assert.Empty(await verify.Set<ProjectEventRevision>().AsNoTracking()
            .Where(x => x.ProjectEventId == eventId).ToListAsync());
        var agreement = await verify.Set<ProjectEventAgreement>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProjectEventId == eventId);
        if (operation == "update-agreement")
        {
            Assert.NotNull(agreement);
            Assert.Equal("Pending", agreement.Status);
            Assert.Equal("Original", agreement.Notes);
            Assert.Equal(1, agreement.Version);
        }
        else
        {
            Assert.Null(agreement);
        }
        Assert.Empty(await verify.Set<ProjectEventAgreementRevision>().AsNoTracking()
            .Where(x => x.ProjectEventId == eventId).ToListAsync());
    }

    [Fact]
    public async Task Owner_WithValidCsrf_CanAppendComment()
    {
        var (projectId, _, eventId) = await SeedAsync();
        await using var factory = new RevestikWebApplicationFactory(
            "Development", fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var token = await GetCsrfAsync(client, OwnerId, "Sales");
        using var request = CreateRequest(HttpMethod.Post,
            $"/api/projects/{projectId}/events/{eventId}/comments",
            new ProjectCommentCreateRequest("Comentario del responsable"));
        Authenticate(request, OwnerId, "Sales");
        request.Headers.Add("X-CSRF-TOKEN", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var verify = fixture.CreateDbContext();
        var comment = Assert.Single(await verify.Set<ProjectEventComment>().AsNoTracking()
            .Where(x => x.ProjectEventId == eventId).ToListAsync());
        Assert.Equal(OwnerId, comment.CreatedByUserId);
    }

    private async Task<(int ProjectId, int CategoryId, int EventId)> SeedAsync(bool withAgreement = false)
    {
        await using var db = fixture.CreateDbContext();
        if (!await db.Users.AnyAsync(x => x.Id == OwnerId))
            db.Users.Add(new ApplicationUser
            {
                Id = OwnerId, UserName = OwnerId, NormalizedUserName = OwnerId.ToUpperInvariant(),
                Email = "b02-http-owner@example.com", NormalizedEmail = "B02-HTTP-OWNER@EXAMPLE.COM",
                DisplayName = "Project owner", SecurityStamp = Guid.NewGuid().ToString()
            });
        var customer = new Customer
        {
            Name = "Cliente pruebas HTTP", IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9],
            Email = "project-http@example.com", PhoneNumber = "88888888",
            ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
            OtherSigns = "Heredia", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var name = Guid.NewGuid().ToString("N");
        var project = new Project
        {
            CustomerId = customer.Id, ResponsibleUserId = OwnerId,
            Name = "Proyecto " + name, NormalizedName = name.ToUpperInvariant()
        };
        var category = new ProjectEventCategory
        {
            Name = "Categoría " + name, NormalizedName = name.ToUpperInvariant()
        };
        db.Set<Project>().Add(project);
        db.Set<ProjectEventCategory>().Add(category);
        await db.SaveChangesAsync();
        var ev = new ProjectEvent
        {
            ProjectId = project.Id, CategoryId = category.Id,
            Title = "Visita original", Description = "Sin cambios",
            OccurredOn = new DateOnly(2026, 10, 1), CreatedByUserId = OwnerId,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Set<ProjectEvent>().Add(ev);
        await db.SaveChangesAsync();
        if (withAgreement)
        {
            db.Set<ProjectEventAgreement>().Add(new ProjectEventAgreement
            {
                ProjectEventId = ev.Id, Status = "Pending", Notes = "Original",
                Version = 1, UpdatedByUserId = OwnerId, UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        return (project.Id, category.Id, ev.Id);
    }

    private static async Task<string> GetCsrfAsync(HttpClient client, string userId, string role)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/csrf");
        Authenticate(request, userId, role);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = body.RootElement.GetProperty("requestToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token;
    }

    private static HttpRequestMessage CreateRequest<T>(HttpMethod method, string path, T body) =>
        new(method, path) { Content = JsonContent.Create(body) };

    private static void Authenticate(HttpRequestMessage request, string userId, string role)
    {
        request.Headers.Add(TestAuthenticationHandler.UserHeaderName, userId);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
    }
}
