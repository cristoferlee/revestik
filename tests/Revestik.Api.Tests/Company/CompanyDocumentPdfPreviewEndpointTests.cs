using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Authentication;

namespace Revestik.Api.Tests.Company;

/// <summary>
/// Tests the actual preview route through the HTTP host, auth, antiforgery and SQL.
/// Each fixture owns its SQL database; no production data is touched.
/// </summary>
public sealed class CompanyDocumentPdfPreviewEndpointTests : IAsyncLifetime
{
    private readonly SqlServerIntegrationTestFixture sql = new();
    private RevestikWebApplicationFactory factory = null!;
    private HttpClient admin = null!;

    private const string Route = "/api/settings/document-pdf/preview";

    public async Task InitializeAsync()
    {
        await sql.InitializeAsync();
        factory = new RevestikWebApplicationFactory("Development", sql.ConnectionString);
        admin = NewClient("Administrator");
    }

    public async Task DisposeAsync()
    {
        admin.Dispose();
        await factory.DisposeAsync();
        await sql.DisposeAsync();
    }

    private HttpClient NewClient(string? role)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "pdf-preview-test-user");
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, role);
        }
        return client;
    }

    private static object ValidRequest(string footer = "Gracias por su preferencia") => new
    {
        footerMessage = footer,
        quotationDefaultTerms = "Sujeto a disponibilidad",
        showWebsite = true,
        showQuotationSeller = true,
        version = (string?)null
    };

    private static async Task<string> CsrfAsync(HttpClient client)
    {
        using var result = await client.GetAsync("/api/auth/csrf");
        result.EnsureSuccessStatusCode();
        var token = await result.Content.ReadFromJsonAsync<CsrfTokenResponse>();
        Assert.NotNull(token);
        return token.RequestToken;
    }

    private static async Task<HttpResponseMessage> PreviewAsync(
        HttpClient client, object body, string? csrf = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Route)
        {
            Content = JsonContent.Create(body)
        };
        if (csrf is not null)
            request.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Preview_RequiresAdministrator()
    {
        using var anonymous = NewClient(null);
        using var unauthenticated = await PreviewAsync(anonymous, ValidRequest());
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        using var sales = NewClient("Sales");
        using var forbidden = await PreviewAsync(sales, ValidRequest());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Preview_RejectsMissingAntiforgeryToken()
    {
        using var result = await PreviewAsync(admin, ValidRequest());
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task Preview_RejectsOversizedText()
    {
        var csrf = await CsrfAsync(admin);
        using var result = await PreviewAsync(admin, ValidRequest(new string('x', 501)), csrf);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task Preview_ReturnsPdfWithoutWritingSettingsOrCreatingDocuments()
    {
        await using var beforeDb = sql.CreateDbContext();
        var quotationsBefore = await beforeDb.Quotations.CountAsync();
        var salesBefore = await beforeDb.Sales.CountAsync();
        var settingsBefore = await beforeDb.CompanyDocumentPdfSettings.CountAsync();
        var auditsBefore = await beforeDb.SecurityAuditEntries.CountAsync();

        var csrf = await CsrfAsync(admin);
        using var result = await PreviewAsync(admin, ValidRequest(), csrf);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("application/pdf", result.Content.Headers.ContentType?.MediaType);
        var bytes = await result.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 100);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));

        await using var afterDb = sql.CreateDbContext();
        Assert.Equal(quotationsBefore, await afterDb.Quotations.CountAsync());
        Assert.Equal(salesBefore, await afterDb.Sales.CountAsync());
        Assert.Equal(settingsBefore, await afterDb.CompanyDocumentPdfSettings.CountAsync());
        Assert.Equal(auditsBefore, await afterDb.SecurityAuditEntries.CountAsync());
    }
}
