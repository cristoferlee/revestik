using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Revestik.Api.Data;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Api.Services.PublicCatalog;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Authentication;

namespace Revestik.Api.Tests.PublicCatalog;

public sealed class PublicMediaMaintenanceSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    [Fact]
    public async Task ConfirmedDeletionRechecksReferencesAndHandlesRepeatedRequest()
    {
        using var factory = new RevestikWebApplicationFactory("Development", fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "media-maintenance-admin");
        var csrf = await client.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf");
        Assert.NotNull(csrf);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.RequestToken);

        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<PublicCatalogMediaStorage>();
        var orphan = CreateFile(storage, -48);
        var referencedLater = CreateFile(storage, -48);
        var recent = CreateFile(storage, -1);
        const string route = "/api/admin/public-media/orphans";
        var scan = await client.GetFromJsonAsync<Discovery>(route);
        Assert.NotNull(scan);
        Assert.Equal(6, scan.TotalBytes);
        Assert.DoesNotContain(scan.Items, x => x.Id == recent.Name);

        var db = scope.ServiceProvider.GetRequiredService<RevestikDbContext>();
        db.PublicHomeHeroSlides.Add(new PublicHomeHeroSlide
        {
            Slot = 1, HeadingEs = "Portada", DescriptionEs = "Imagen de portada",
            ImageStorageKey = referencedLater.Name, ImageContentType = "image/webp",
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var tickets = scan.Items.Select(x => x.Ticket).ToArray();
        using var unconfirmed = await client.PostAsJsonAsync(route + "/delete", new { confirmed = false, tickets });
        Assert.Equal(HttpStatusCode.BadRequest, unconfirmed.StatusCode);
        var first = await Delete();
        Assert.Equal("deleted", first[orphan.Name]);
        Assert.Equal("skipped", first[referencedLater.Name]);
        Assert.True(File.Exists(referencedLater.FullName));
        Assert.True(File.Exists(recent.FullName));
        Assert.Equal("missing", (await Delete())[orphan.Name]);

        async Task<Dictionary<string, string>> Delete()
        {
            using var response = await client.PostAsJsonAsync(route + "/delete", new { confirmed = true, tickets });
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<Deletion>();
            Assert.NotNull(body);
            return body.Results.ToDictionary(x => x.Id, x => x.Status);
        }
    }

    private static FileInfo CreateFile(PublicCatalogMediaStorage storage, int ageHours)
    {
        var file = storage.GetActiveInfo(Guid.NewGuid().ToString("N") + ".webp");
        Directory.CreateDirectory(file.DirectoryName!);
        File.WriteAllBytes(file.FullName, [1, 2, 3]);
        File.SetLastWriteTimeUtc(file.FullName, DateTime.UtcNow.AddHours(ageHours));
        return file;
    }

    private sealed record Discovery(Candidate[] Items, long TotalBytes);
    private sealed record Candidate(string Id, string Ticket);
    private sealed record Deletion(Outcome[] Results);
    private sealed record Outcome(string Id, string Status);
}
