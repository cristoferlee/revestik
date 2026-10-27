using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Revestik.Api.Services.Company;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Authentication;
using Revestik.Shared.Company;

namespace Revestik.Api.Tests.Company;

public sealed class CompanyLogoSecurityTests : IAsyncLifetime
{
    private readonly SqlServerIntegrationTestFixture sql = new();
    private RevestikWebApplicationFactory factory = null!;
    private HttpClient admin = null!;

    // A complete, minimal PNG used for accepted-upload tests.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=");

    public async Task InitializeAsync()
    {
        await sql.InitializeAsync();
        factory = new RevestikWebApplicationFactory("Development", sql.ConnectionString);
        admin = Client("Administrator");
    }

    public async Task DisposeAsync()
    {
        admin.Dispose();
        await factory.DisposeAsync();
        await sql.DisposeAsync();
    }

    private HttpClient Client(string? role)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "company-logo-test-user");
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, role);
        }
        return client;
    }

    private static MultipartFormDataContent Form(byte[] bytes, string fileName, string version)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", fileName);
        form.Add(new StringContent(version), "version");
        return form;
    }

    private static async Task<string> CsrfAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<CsrfTokenResponse>();
        Assert.NotNull(token);
        return token.RequestToken;
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] bytes, string name,
        string version, string? csrf)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/settings/company/logo")
        {
            Content = Form(bytes, name, version)
        };
        if (csrf is not null) request.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(request);
    }

    [Fact]
    public void Validation_RejectsIncorrectExtensionAndOversizedPayload()
    {
        Assert.Throws<InvalidDataException>(() => CompanyLogoStorage.Validate(Png, "logo.svg"));
        Assert.Throws<InvalidDataException>(() => CompanyLogoStorage.Validate(new byte[CompanyLogoStorage.MaxBytes + 1], "logo.png"));
    }

    [Fact]
    public void Validation_RejectsInvalidPngSignatureAndMissingFooter()
    {
        var badSignature = (byte[])Png.Clone();
        badSignature[0] = 0;
        Assert.Throws<InvalidDataException>(() => CompanyLogoStorage.Validate(badSignature, "logo.png"));
        Assert.Throws<InvalidDataException>(() => CompanyLogoStorage.Validate(Png[..^8], "logo.png"));
    }

    [Fact]
    public async Task LogoEndpoints_RejectAnonymousAndNonAdministrator()
    {
        using var anonymous = Client(null);
        using var forbidden = Client("Accountant");
        using var anonymousRead = await anonymous.GetAsync("/api/settings/company/logo");
        using var forbiddenRead = await forbidden.GetAsync("/api/settings/company/logo");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousRead.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRead.StatusCode);
    }

    [Fact]
    public async Task Upload_RejectsMissingCsrfAndLeavesDefaultLogo()
    {
        var company = await admin.GetFromJsonAsync<CompanyProfileResponse>("/api/settings/company");
        Assert.NotNull(company);
        using var response = await UploadAsync(admin, Png, "logo.png", company.Version, null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = sql.CreateDbContext();
        var record = await db.CompanyProfiles.SingleAsync();
        Assert.Null(record.LogoStorageKey);
    }

    [Fact]
    public async Task Upload_RejectsOversizedAndUnsupportedFilesWithValidCsrf()
    {
        var company = await admin.GetFromJsonAsync<CompanyProfileResponse>("/api/settings/company");
        Assert.NotNull(company);
        var csrf = await CsrfAsync(admin);
        using var invalid = await UploadAsync(admin, Png, "logo.svg", company.Version, csrf);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var huge = await UploadAsync(admin, new byte[CompanyLogoStorage.MaxBytes + 1], "logo.png", company.Version, csrf);
        Assert.Equal(HttpStatusCode.BadRequest, huge.StatusCode);
    }

    [Fact]
    public async Task Restore_RejectsMissingCsrfAndInvalidVersion()
    {
        var company = await admin.GetFromJsonAsync<CompanyProfileResponse>("/api/settings/company");
        Assert.NotNull(company);
        using var missing = new HttpRequestMessage(HttpMethod.Delete, "/api/settings/company/logo");
        missing.Headers.Add("X-Company-Version", company.Version);
        using var missingResponse = await admin.SendAsync(missing);
        Assert.Equal(HttpStatusCode.BadRequest, missingResponse.StatusCode);

        var csrf = await CsrfAsync(admin);
        using var invalid = new HttpRequestMessage(HttpMethod.Delete, "/api/settings/company/logo");
        invalid.Headers.Add("X-CSRF-TOKEN", csrf);
        invalid.Headers.Add("X-Company-Version", "invalid-version");
        using var invalidResponse = await admin.SendAsync(invalid);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
    }

    private static async Task<string> ResponseVersionAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var json = await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("version").GetString()!;
    }

    private static async Task<HttpResponseMessage> RestoreAsync(HttpClient client, string version, string csrf)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/settings/company/logo");
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.Add("X-Company-Version", version);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Upload_Read_Replace_Restore_RetainsHistoricalFiles()
    {
        var company = await admin.GetFromJsonAsync<CompanyProfileResponse>("/api/settings/company");
        Assert.NotNull(company);
        var csrf = await CsrfAsync(admin);

        using var initial = await UploadAsync(admin, Png, "first.png", company.Version, csrf);
        var firstVersion = await ResponseVersionAsync(initial);
        string firstKey;
        await using (var db = sql.CreateDbContext())
        {
            var profile = await db.CompanyProfiles.SingleAsync();
            firstKey = Assert.IsType<string>(profile.LogoStorageKey);
            Assert.Equal("image/png", profile.LogoContentType);
        }
        using (var image = await admin.GetAsync("/api/settings/company/logo"))
        {
            Assert.Equal(HttpStatusCode.OK, image.StatusCode);
            Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
            Assert.Equal(Png, await image.Content.ReadAsByteArrayAsync());
        }

        using var replacement = await UploadAsync(admin, Png, "replacement.png", firstVersion, csrf);
        var secondVersion = await ResponseVersionAsync(replacement);
        string secondKey;
        await using (var db = sql.CreateDbContext())
        {
            var profile = await db.CompanyProfiles.SingleAsync();
            secondKey = Assert.IsType<string>(profile.LogoStorageKey);
            Assert.NotEqual(firstKey, secondKey);
        }
        // Historical revisions must remain accessible to CONFIG-02D snapshots.
        var storage = factory.Services.GetRequiredService<CompanyLogoStorage>();
        Assert.Equal(Png, await storage.ReadAsync(firstKey, CancellationToken.None));
        Assert.Equal(Png, await storage.ReadAsync(secondKey, CancellationToken.None));

        using var restored = await RestoreAsync(admin, secondVersion, csrf);
        _ = await ResponseVersionAsync(restored);
        await using (var db = sql.CreateDbContext())
        {
            var profile = await db.CompanyProfiles.SingleAsync();
            Assert.Null(profile.LogoStorageKey);
            Assert.Null(profile.LogoContentType);
        }
        Assert.Equal(Png, await storage.ReadAsync(firstKey, CancellationToken.None));
        Assert.Equal(Png, await storage.ReadAsync(secondKey, CancellationToken.None));
        using var defaultImage = await admin.GetAsync("/api/settings/company/logo");
        Assert.Equal(HttpStatusCode.OK, defaultImage.StatusCode);
        Assert.Equal("image/svg+xml", defaultImage.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Upload_RejectsStaleVersion_WithoutReplacingActiveLogo()
    {
        var company = await admin.GetFromJsonAsync<CompanyProfileResponse>("/api/settings/company");
        Assert.NotNull(company);
        var csrf = await CsrfAsync(admin);
        using var upload = await UploadAsync(admin, Png, "first.png", company.Version, csrf);
        var currentVersion = await ResponseVersionAsync(upload);
        string key;
        await using (var db = sql.CreateDbContext())
            key = Assert.IsType<string>((await db.CompanyProfiles.SingleAsync()).LogoStorageKey);

        using var staleUpload = await UploadAsync(admin, Png, "stale.png", company.Version, csrf);
        Assert.Equal(HttpStatusCode.Conflict, staleUpload.StatusCode);
        using var staleRestore = await RestoreAsync(admin, company.Version, csrf);
        Assert.Equal(HttpStatusCode.Conflict, staleRestore.StatusCode);
        await using (var db = sql.CreateDbContext())
            Assert.Equal(key, (await db.CompanyProfiles.SingleAsync()).LogoStorageKey);

        using var validRestore = await RestoreAsync(admin, currentVersion, csrf);
        Assert.Equal(HttpStatusCode.OK, validRestore.StatusCode);
    }
}
