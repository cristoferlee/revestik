using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Revestik.Api.Data;
using Revestik.Api.Integrations.Locations;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Authentication;
using Revestik.Shared.Company;
using Revestik.Shared.Locations;

namespace Revestik.Api.Tests.Company;

public sealed class CompanyProfileEndpointTests : IAsyncLifetime
{
    private readonly SqlServerIntegrationTestFixture sql = new();
    private RevestikWebApplicationFactory factory = null!;
    private HttpClient admin = null!;

    public async Task InitializeAsync()
    {
        await sql.InitializeAsync();
        factory = new RevestikWebApplicationFactory("Development", sql.ConnectionString,
            customizeTestServices: services =>
            {
                services.RemoveAll<ILocationCatalogClient>();
                services.AddSingleton<ILocationCatalogClient>(new FixedLocationCatalog());
            });
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
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeaderName, "config-test-user");
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, role);
        }
        return client;
    }

    private static UpdateCompanyProfileRequest Update(string version, string name = "Nueva marca") => new(
        name, "88888888", "empresa@example.com", "https://example.com",
        "4", "01", "01", "Dirección para pruebas", version);

    private static async Task<string> CsrfAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CsrfTokenResponse>();
        Assert.NotNull(payload);
        return payload.RequestToken;
    }

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client,
        UpdateCompanyProfileRequest update, string? csrf = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/settings/company")
        {
            Content = JsonContent.Create(update)
        };
        if (csrf is not null) request.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Get_InitializesOnceAndReturnsLegalIdentity()
    {
        using var first = await admin.GetAsync("/api/settings/company");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var company = await first.Content.ReadFromJsonAsync<CompanyProfileResponse>();
        Assert.NotNull(company);
        Assert.False(string.IsNullOrWhiteSpace(company.LegalName));
        Assert.False(string.IsNullOrWhiteSpace(company.TaxIdentificationNumber));
        Assert.False(string.IsNullOrWhiteSpace(company.Version));

        using var second = await admin.GetAsync("/api/settings/company");
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        await using var db = sql.CreateDbContext();
        Assert.Equal(1, await db.CompanyProfiles.CountAsync());
    }

    [Fact]
    public async Task Get_RequiresAdministrator()
    {
        using var anonymous = NewClient(null);
        using var unauthenticated = await anonymous.GetAsync("/api/settings/company");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        using var accountant = NewClient("Accountant");
        using var forbidden = await accountant.GetAsync("/api/settings/company");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Put_RejectsMissingCsrfAndNonAdministrator()
    {
        var current = await admin.GetFromJsonAsync<CompanyProfileResponse>("/api/settings/company");
        Assert.NotNull(current);
        using var noCsrf = await PutAsync(admin, Update(current.Version));
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);

        using var accountant = NewClient("Accountant");
        using var forbidden = await PutAsync(accountant, Update(current.Version));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var again = await admin.GetFromJsonAsync<CompanyProfileResponse>("/api/settings/company");
        Assert.NotNull(again);
        Assert.Equal(current.CommercialName, again.CommercialName);
    }

    [Fact]
    public async Task Put_UpdatesCommercialDataButPreservesFiscalIdentityAndDetectsStaleVersion()
    {
        var initial = await admin.GetFromJsonAsync<CompanyProfileResponse>("/api/settings/company");
        Assert.NotNull(initial);
        var token = await CsrfAsync(admin);
        using var updated = await PutAsync(admin, Update(initial.Version), token);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var result = await updated.Content.ReadFromJsonAsync<CompanyProfileResponse>();
        Assert.NotNull(result);
        Assert.Equal("Nueva marca", result.CommercialName);
        Assert.Equal("empresa@example.com", result.Email);
        Assert.Equal("01", result.DistrictCode);
        Assert.Contains("Dirección para pruebas", result.Address);
        Assert.Equal(initial.LegalName, result.LegalName);
        Assert.Equal(initial.TaxIdentificationNumber, result.TaxIdentificationNumber);
        Assert.NotEqual(initial.Version, result.Version);

        await using (var db = sql.CreateDbContext())
        {
            var persisted = await db.CompanyProfiles.SingleAsync();
            Assert.Equal("Nueva marca", persisted.CommercialName);
            Assert.Equal(initial.LegalName, persisted.LegalName);
        }
        using var stale = await PutAsync(admin, Update(initial.Version, "Sobrescritura"), token);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var current = await admin.GetFromJsonAsync<CompanyProfileResponse>("/api/settings/company");
        Assert.NotNull(current);
        Assert.Equal("Nueva marca", current.CommercialName);
    }

    [Fact]
    public async Task Put_RejectsInvalidInputWithoutMutation()
    {
        var initial = await admin.GetFromJsonAsync<CompanyProfileResponse>("/api/settings/company");
        Assert.NotNull(initial);
        var token = await CsrfAsync(admin);
        using var invalidEmail = await PutAsync(admin, Update(initial.Version) with { Email = "invalid" }, token);
        Assert.Equal(HttpStatusCode.BadRequest, invalidEmail.StatusCode);
        using var invalidCanton = await PutAsync(admin, Update(initial.Version) with { CantonCode = "99" }, token);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCanton.StatusCode);
        using var invalidVersion = await PutAsync(admin, Update("invalid-version"), token);
        Assert.Equal(HttpStatusCode.BadRequest, invalidVersion.StatusCode);
        var current = await admin.GetFromJsonAsync<CompanyProfileResponse>("/api/settings/company");
        Assert.NotNull(current);
        Assert.Equal(initial.Version, current.Version);
    }

    private sealed class FixedLocationCatalog : ILocationCatalogClient
    {
        public Task<LocationCatalogResult> GetProvincesAsync(CancellationToken ct = default) =>
            Task.FromResult(Available(new LocationOptionResponse("4", "Heredia")));

        public Task<LocationCatalogResult> GetCantonsAsync(string provinceCode, CancellationToken ct = default) =>
            Task.FromResult(provinceCode == "4" ? Available(new LocationOptionResponse("01", "Heredia")) : NotFound());

        public Task<LocationCatalogResult> GetDistrictsAsync(string provinceCode, string cantonCode, CancellationToken ct = default) =>
            Task.FromResult(provinceCode == "4" && cantonCode == "01"
                ? Available(new LocationOptionResponse("01", "Heredia")) : NotFound());

        private static LocationCatalogResult Available(params LocationOptionResponse[] entries) =>
            new(LocationCatalogStatus.Available, entries);
        private static LocationCatalogResult NotFound() => new(LocationCatalogStatus.NotFound, []);
    }
}
