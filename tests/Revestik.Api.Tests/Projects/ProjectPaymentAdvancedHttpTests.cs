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

public sealed class ProjectPaymentAdvancedHttpTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string Owner = "b05d-http-owner";
    private const string Other = "b05d-http-other";

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
            Name = "B05D HTTP", IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9],
            Email = "b05d-http@example.com", PhoneNumber = "88888888",
            ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
            OtherSigns = "Heredia", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var name = Guid.NewGuid().ToString("N");
        var project = new Project
        {
            CustomerId = customer.Id, ResponsibleUserId = Owner,
            Name = name, NormalizedName = name.ToUpperInvariant(), AgreedAmount = 100m, CurrencyCode = "CRC"
        };
        db.Set<Project>().Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    private static void Auth(HttpRequestMessage req, string user, string role)
    {
        req.Headers.Add(TestAuthenticationHandler.UserHeaderName, user);
        req.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
    }

    private static async Task<string> Csrf(HttpClient client, string user, string role)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/csrf");
        Auth(request, user, role);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("requestToken").GetString()!;
    }

    [Fact]
    public async Task OwnerWithValidCsrf_CreatesPayment_AndNonOwnerCannotMutate()
    {
        var projectId = await SeedAsync();
        await using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
        });
        var ownerCsrf = await Csrf(client, Owner, "Sales");
        using (var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/payments")
        {
            Content = JsonContent.Create(new ProjectPaymentUpsertRequest(40m, "CRC", new DateOnly(2026, 10, 16), "SINPE", ""))
        })
        {
            Auth(request, Owner, "Sales");
            request.Headers.Add("X-CSRF-TOKEN", ownerCsrf);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        var otherCsrf = await Csrf(client, Other, "Sales");
        using (var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/payments")
        {
            Content = JsonContent.Create(new ProjectPaymentUpsertRequest(20m, "CRC", new DateOnly(2026, 10, 16), "", ""))
        })
        {
            Auth(request, Other, "Sales");
            request.Headers.Add("X-CSRF-TOKEN", otherCsrf);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        await using var db = fixture.CreateDbContext();
        Assert.Equal(40m, await db.Set<ProjectPayment>()
            .Where(x => x.ProjectId == projectId && x.Status == "Active")
            .SumAsync(x => x.Amount));
    }
}
