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

public sealed class ProjectDocumentHttpTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>, IAsyncLifetime
{
    private const string Owner = "b04-http-owner";
    private const string Other = "b04-http-other";
    private readonly string directory = Path.Combine(Path.GetTempPath(), "revestik-b04-http-" + Guid.NewGuid().ToString("N"));

    private async Task<int> SeedAsync()
    {
        await using var db = fixture.CreateDbContext();
        if (!await db.Users.AnyAsync(x => x.Id == Owner))
            db.Users.Add(new ApplicationUser { Id = Owner, UserName = Owner,
                NormalizedUserName = Owner.ToUpperInvariant(), Email = "b04-http-owner@example.com",
                NormalizedEmail = "B04-HTTP-OWNER@EXAMPLE.COM", DisplayName = "Owner",
                SecurityStamp = Guid.NewGuid().ToString() });
        var customer = new Customer
        {
            Name = "Cliente B04 HTTP", IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9],
            Email = "b04-http@example.com", PhoneNumber = "88888888",
            ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
            OtherSigns = "Heredia", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var project = new Project { CustomerId = customer.Id, ResponsibleUserId = Owner,
            Name = "Proyecto " + suffix, NormalizedName = suffix.ToUpperInvariant() };
        db.Set<Project>().Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    private RevestikWebApplicationFactory? documentFactory;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (documentFactory is not null)
            await documentFactory.DisposeAsync();
    }

    private HttpClient CreateClient()
    {
        documentFactory ??= new RevestikWebApplicationFactory("Development", fixture.ConnectionString, directory);
        return documentFactory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost"),
                HandleCookies = true
            });
    }

    private static void Login(HttpRequestMessage request, string actor = Owner, string role = "Sales")
    {
        request.Headers.Add(TestAuthenticationHandler.UserHeaderName, actor);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
    }

    private static async Task<string> CsrfAsync(HttpClient client, string actor = Owner, string role = "Sales")
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/auth/csrf");
        Login(req, actor, role);
        using var res = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("requestToken").GetString()!;
    }

    private static byte[] Pdf() => Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF");

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, int projectId,
        byte[] bytes, string filename, string actor, string role, string? csrf)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Documento de prueba"), "displayName");
        form.Add(new StringContent("Plan"), "category");
        var payload = new ByteArrayContent(bytes);
        payload.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        form.Add(payload, "file", filename);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/documents")
        { Content = form };
        Login(request, actor, role);
        if (csrf is not null) request.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task ReadRequiresAuthenticationAndRole()
    {
        var id = await SeedAsync();
        using var client = CreateClient();
        using var anonymous = await client.GetAsync($"/api/projects/{id}/documents");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{id}/documents");
        Login(request, Other, "Warehouse");
        using var forbidden = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task OwnerUpload_List_Details_Download_Preview_Work()
    {
        var projectId = await SeedAsync();
        using var client = CreateClient();
        var token = await CsrfAsync(client);
        var content = Pdf();
        using var upload = await UploadAsync(client, projectId, content, "plano.pdf", Owner, "Sales", token);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        using var result = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var id = result.RootElement.GetProperty("id").GetInt32();
        Assert.True(id > 0);
        using var listRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}/documents");
        Login(listRequest, Other, "Accountant");
        using var list = await client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listing = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Single(listing.RootElement.EnumerateArray());
        using var detailRequest = new HttpRequestMessage(HttpMethod.Get,
            $"/api/projects/{projectId}/documents/{id}");
        Login(detailRequest);
        using var details = await client.SendAsync(detailRequest);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        using var detailJson = JsonDocument.Parse(await details.Content.ReadAsStringAsync());
        Assert.Equal(1, detailJson.RootElement.GetProperty("versions").GetArrayLength());
        var versionId = detailJson.RootElement.GetProperty("versions")[0].GetProperty("id").GetInt32();
        foreach (var url in new[] {
            $"/api/projects/{projectId}/documents/{id}/content",
            $"/api/projects/{projectId}/documents/{id}/preview",
            $"/api/projects/{projectId}/documents/{id}/versions/{versionId}/content" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            Login(request);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(content, await response.Content.ReadAsByteArrayAsync());
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        }
        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, await db.Set<ProjectDocumentVersion>().CountAsync(x => x.ProjectDocumentId == id));
    }

    [Fact]
    public async Task UploadRequiresCsrfAndProjectOwner()
    {
        var id = await SeedAsync();
        using var client = CreateClient();
        using var noCsrf = await UploadAsync(client, id, Pdf(), "plan.pdf", Owner, "Sales", null);
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        var token = await CsrfAsync(client, Other, "Accountant");
        using var outsider = await UploadAsync(client, id, Pdf(), "plan.pdf", Other, "Accountant", token);
        Assert.Equal(HttpStatusCode.Forbidden, outsider.StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.False(await db.Set<ProjectDocument>().AnyAsync(x => x.ProjectId == id));
    }

    [Fact]
    public async Task FakePdfAndInvalidFileName_AreRejectedWithoutPersisting()
    {
        var id = await SeedAsync();
        using var client = CreateClient();
        var token = await CsrfAsync(client);
        using var fake = await UploadAsync(client, id, Encoding.UTF8.GetBytes("not a PDF"),
            "fake.pdf", Owner, "Sales", token);
        Assert.Equal(HttpStatusCode.BadRequest, fake.StatusCode);
        using var wrongExtension = await UploadAsync(client, id, Pdf(),
            "file.exe", Owner, "Sales", token);
        Assert.Equal(HttpStatusCode.BadRequest, wrongExtension.StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.False(await db.Set<ProjectDocument>().AnyAsync(x => x.ProjectId == id));
    }

    private static async Task<HttpResponseMessage> AddVersionHttpAsync(HttpClient client,
        int projectId, int documentId, long expectedVersion, byte[] bytes,
        string actor, string role, string? csrf)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(expectedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            "expectedDocumentVersion");
        form.Add(new ByteArrayContent(bytes), "file", "actualizado.pdf");
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"/api/projects/{projectId}/documents/{documentId}/versions") { Content = form };
        Login(req, actor, role);
        if (csrf is not null) req.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> MutationAsync(HttpClient client, HttpMethod method,
        string path, string actor, string role, string? csrf)
    {
        using var request = new HttpRequestMessage(method, path);
        Login(request, actor, role);
        if (csrf is not null) request.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Versioning_RetainsHistory_AndRejectsStaleWrites()
    {
        var projectId = await SeedAsync();
        using var client = CreateClient();
        var csrf = await CsrfAsync(client);
        using var created = await UploadAsync(client, projectId, Pdf(), "original.pdf", Owner, "Sales", csrf);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdJson.RootElement.GetProperty("id").GetInt32();
        var documentVersion = createdJson.RootElement.GetProperty("version").GetInt64();
        var initialId = createdJson.RootElement.GetProperty("currentVersionId").GetInt32();
        var updatedBytes = Encoding.ASCII.GetBytes("%PDF-1.7\n2 0 obj\n<<>>\nendobj\n%%EOF");
        using var changed = await AddVersionHttpAsync(client, projectId, id, documentVersion,
            updatedBytes, Owner, "Sales", csrf);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using var changedJson = JsonDocument.Parse(await changed.Content.ReadAsStringAsync());
        var nextId = changedJson.RootElement.GetProperty("currentVersionId").GetInt32();
        Assert.NotEqual(initialId, nextId);
        using var stale = await AddVersionHttpAsync(client, projectId, id, documentVersion,
            Pdf(), Owner, "Sales", csrf);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var currentRequest = new HttpRequestMessage(HttpMethod.Get,
            $"/api/projects/{projectId}/documents/{id}/content");
        Login(currentRequest);
        using var current = await client.SendAsync(currentRequest);
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        Assert.Equal(updatedBytes, await current.Content.ReadAsByteArrayAsync());
        using var historicalRequest = new HttpRequestMessage(HttpMethod.Get,
            $"/api/projects/{projectId}/documents/{id}/versions/{initialId}/content");
        Login(historicalRequest);
        using var historical = await client.SendAsync(historicalRequest);
        Assert.Equal(HttpStatusCode.OK, historical.StatusCode);
        Assert.Equal(Pdf(), await historical.Content.ReadAsByteArrayAsync());
        await using var db = fixture.CreateDbContext();
        Assert.Equal(2, await db.Set<ProjectDocumentVersion>().CountAsync(x => x.ProjectDocumentId == id));
    }

    [Fact]
    public async Task VersionWrite_RequiresCsrfAndResponsibleUser()
    {
        var projectId = await SeedAsync();
        using var client = CreateClient();
        var csrf = await CsrfAsync(client);
        using var created = await UploadAsync(client, projectId, Pdf(), "base.pdf", Owner, "Sales", csrf);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("id").GetInt32();
        var version = json.RootElement.GetProperty("version").GetInt64();
        using var withoutCsrf = await AddVersionHttpAsync(client, projectId, id, version,
            Pdf(), Owner, "Sales", null);
        Assert.Equal(HttpStatusCode.BadRequest, withoutCsrf.StatusCode);
        using var outsider = await AddVersionHttpAsync(client, projectId, id, version,
            Pdf(), Other, "Accountant", await CsrfAsync(client, Other, "Accountant"));
        Assert.Equal(HttpStatusCode.Forbidden, outsider.StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, await db.Set<ProjectDocumentVersion>().CountAsync(x => x.ProjectDocumentId == id));
    }

    [Fact]
    public async Task SoftDelete_HidesDocuments_WithoutDeletingVersionFiles()
    {
        var projectId = await SeedAsync();
        using var client = CreateClient();
        var csrf = await CsrfAsync(client);
        using var created = await UploadAsync(client, projectId, Pdf(), "base.pdf", Owner, "Sales", csrf);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("id").GetInt32();
        var version = json.RootElement.GetProperty("version").GetInt64();
        var path = $"/api/projects/{projectId}/documents/{id}";
        using var forbidden = await MutationAsync(client, HttpMethod.Delete,
            path + $"?expectedDocumentVersion={version}", Other, "Accountant",
            await CsrfAsync(client, Other, "Accountant"));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var stale = await MutationAsync(client, HttpMethod.Delete,
            path + "?expectedDocumentVersion=99999", Owner, "Sales", csrf);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var deleted = await MutationAsync(client, HttpMethod.Delete,
            path + $"?expectedDocumentVersion={version}", Owner, "Sales", csrf);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var detail = await MutationAsync(client, HttpMethod.Get, path, Owner, "Sales", null);
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        using var download = await MutationAsync(client, HttpMethod.Get, path + "/content", Owner, "Sales", null);
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
        await using var db = fixture.CreateDbContext();
        var doc = await db.Set<ProjectDocument>().SingleAsync(x => x.Id == id);
        Assert.NotNull(doc.DeletedAtUtc);
        var storedVersion = await db.Set<ProjectDocumentVersion>().SingleAsync(x => x.ProjectDocumentId == id);
        Assert.False(string.IsNullOrWhiteSpace(storedVersion.StorageKey));
        Assert.True(File.Exists(Path.Combine(directory, storedVersion.StorageKey)));
    }

    [Fact]
    public async Task LinkEvent_RejectsForeignOrUnknownEvent_AndRequiresCsrf()
    {
        var projectId = await SeedAsync();
        using var client = CreateClient();
        var csrf = await CsrfAsync(client);
        using var created = await UploadAsync(client, projectId, Pdf(), "evidence.pdf", Owner, "Sales", csrf);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("id").GetInt32();
        var path = $"/api/projects/{projectId}/documents/{id}/events/2147483647";
        using var noCsrf = await MutationAsync(client, HttpMethod.Post, path, Owner, "Sales", null);
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        using var foreign = await MutationAsync(client, HttpMethod.Post, path, Owner, "Sales", csrf);
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        using var unlink = await MutationAsync(client, HttpMethod.Delete, path, Owner, "Sales", csrf);
        Assert.Equal(HttpStatusCode.NoContent, unlink.StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.False(await db.Set<ProjectDocumentEventLink>().AnyAsync(x => x.ProjectDocumentId == id));
    }

    [Fact]
    public async Task ReadCannotCrossProjectOrGuessStorageKey()
    {
        var project1 = await SeedAsync();
        var project2 = await SeedAsync();
        using var client = CreateClient();
        var token = await CsrfAsync(client);
        using var uploaded = await UploadAsync(client, project1, Pdf(), "plan.pdf", Owner, "Sales", token);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        using var data = JsonDocument.Parse(await uploaded.Content.ReadAsStringAsync());
        var documentId = data.RootElement.GetProperty("id").GetInt32();
        using var wrong = new HttpRequestMessage(HttpMethod.Get,
            $"/api/projects/{project2}/documents/{documentId}/content");
        Login(wrong);
        using var denied = await client.SendAsync(wrong);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }
}
