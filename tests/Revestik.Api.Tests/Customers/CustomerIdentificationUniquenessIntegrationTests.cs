using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Authentication;
using Revestik.Shared.Customers;

namespace Revestik.Api.Tests.Customers;

public sealed class CustomerIdentificationUniquenessIntegrationTests(
    SqlServerIntegrationTestFixture sqlServerFixture)
    : IClassFixture<SqlServerIntegrationTestFixture>, IAsyncLifetime
{
    private RevestikWebApplicationFactory factory = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        await using (var dbContext = sqlServerFixture.CreateDbContext())
        {
            await dbContext.Customers.ExecuteDeleteAsync();
        }

        factory = new RevestikWebApplicationFactory(
            "Development",
            sqlServerFixture.ConnectionString);

        client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true
            });

        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.UserHeaderName,
            "customer-uniqueness-test-user");
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task CreateCustomer_WithDuplicateIdentification_ReturnsConflict()
    {
        await SeedCustomerAsync(
            "Existing Customer",
            "123456789");

        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/customers",
            CreateRequest(
                "Duplicate Customer",
                "123456789"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var dbContext = sqlServerFixture.CreateDbContext();
        Assert.Equal(
            1,
            await dbContext.Customers.CountAsync(
                customer => customer.IdentificationNumber ==
                    "123456789"));
    }

    [Fact]
    public async Task UpdateCustomer_WithDuplicateIdentification_ReturnsConflict()
    {
        var existingCustomerId = await SeedCustomerAsync(
            "Existing Customer",
            "123456789");
        var customerToUpdateId = await SeedCustomerAsync(
            "Customer To Update",
            "234567890");

        using var response = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/customers/{customerToUpdateId}",
            CreateRequest(
                "Updated Customer",
                "123456789"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var dbContext = sqlServerFixture.CreateDbContext();
        var persistedCustomer = await dbContext.Customers
            .AsNoTracking()
            .SingleAsync(customer => customer.Id == customerToUpdateId);

        Assert.Equal("234567890", persistedCustomer.IdentificationNumber);
        Assert.NotEqual(existingCustomerId, persistedCustomer.Id);
    }

    private async Task<HttpResponseMessage> SendWithCsrfAsync(
        HttpMethod method,
        string path,
        CustomerUpsertRequest requestPayload)
    {
        var csrfToken = await GetCsrfTokenAsync();

        using var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(requestPayload)
        };

        request.Headers.Add("X-CSRF-TOKEN", csrfToken);

        return await client.SendAsync(request);
    }

    private async Task<string> GetCsrfTokenAsync()
    {
        using var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync<CsrfTokenResponse>();

        Assert.NotNull(payload);
        return payload.RequestToken;
    }

    private async Task<int> SeedCustomerAsync(
        string name,
        string identificationNumber)
    {
        await using var dbContext = sqlServerFixture.CreateDbContext();

        var customer = new Customer
        {
            Name = name,
            IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = identificationNumber,
            Email = $"{identificationNumber}@example.com",
            PhoneNumber = "88888888",
            ProvinceCode = "4",
            CantonCode = "01",
            DistrictCode = "01",
            OtherSigns = "Integration test address",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        return customer.Id;
    }

    private static CustomerUpsertRequest CreateRequest(
        string name,
        string identificationNumber)
    {
        return new CustomerUpsertRequest
        {
            Name = name,
            IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = identificationNumber,
            Email = "customer@example.com",
            PhoneNumber = "88888888",
            ProvinceCode = "4",
            CantonCode = "01",
            DistrictCode = "01",
            OtherSigns = "Integration test address",
            IsActive = true
        };
    }
}
