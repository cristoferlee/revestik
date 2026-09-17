using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Authentication;
using Revestik.Shared.Customers;
using Revestik.Shared.Quotations;

namespace Revestik.Api.Tests.Quotations;

public sealed class QuotationEndpointTests(
    SqlServerIntegrationTestFixture sqlServerFixture)
    : IClassFixture<SqlServerIntegrationTestFixture>, IAsyncLifetime
{
    private RevestikWebApplicationFactory factory = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        await using (var dbContext =
            sqlServerFixture.CreateDbContext())
        {
            await dbContext.QuotationCharges.ExecuteDeleteAsync();
            await dbContext.QuotationLines.ExecuteDeleteAsync();
            await dbContext.Quotations.ExecuteDeleteAsync();
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
            "quotation-endpoint-test-user");
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task GetQuotation_WhenQuotationExists_ReturnsOk()
    {
        var customerId = await SeedCustomerAsync();

        var quotationId = await SeedQuotationAsync(
            customerId,
            "COT-900001");

        using var response = await client.GetAsync(
            $"/api/quotations/{quotationId}");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var quotation =
            await response.Content
                .ReadFromJsonAsync<QuotationResponse>();

        Assert.NotNull(quotation);

        Assert.Equal(
            quotationId,
            quotation.Id);

        Assert.Equal(
            "COT-900001",
            quotation.QuotationNumber);

        Assert.Equal(
            customerId,
            quotation.CustomerId);
    }

    [Fact]
    public async Task GetQuotation_WhenQuotationDoesNotExist_ReturnsNotFound()
    {
        using var response = await client.GetAsync(
            "/api/quotations/999999");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateQuotation_WithValidRequest_ReturnsCreated()
    {
        var customerId = await SeedCustomerAsync();

        var requestPayload =
            CreateValidQuotationRequest(customerId);

        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/quotations",
            requestPayload);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var quotation =
            await response.Content
                .ReadFromJsonAsync<QuotationResponse>();

        Assert.NotNull(quotation);
        Assert.True(quotation.Id > 0);

        Assert.Equal(
            customerId,
            quotation.CustomerId);

        Assert.StartsWith(
            "COT-",
            quotation.QuotationNumber);

        Assert.Equal(
            $"/api/quotations/{quotation.Id}",
            response.Headers.Location?.ToString());

        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        var persistedQuotation =
            await dbContext.Quotations
                .AsNoTracking()
                .SingleAsync(
                    item => item.Id == quotation.Id);

        Assert.Equal(
            quotation.QuotationNumber,
            persistedQuotation.QuotationNumber);

        Assert.Equal(
            customerId,
            persistedQuotation.CustomerId);
    }

    [Fact]
    public async Task CreateQuotation_WithMissingCustomer_ReturnsBadRequest()
    {
        var requestPayload =
            CreateValidQuotationRequest(999999);

        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/quotations",
            requestPayload);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        Assert.False(
            await dbContext.Quotations.AnyAsync());
    }

    [Fact]
    public async Task CreateQuotation_WithInvalidRequest_ReturnsBadRequest()
    {
        var customerId = await SeedCustomerAsync();

        var requestPayload =
            CreateValidQuotationRequest(customerId);

        requestPayload.Lines[0].CabysCode = string.Empty;

        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/quotations",
            requestPayload);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        Assert.False(
            await dbContext.Quotations.AnyAsync());
    }

    [Fact]
    public async Task UpdateQuotation_WithValidRequest_ReturnsOk()
    {
        var customerId = await SeedCustomerAsync();

        var quotationId = await SeedQuotationAsync(
            customerId,
            "COT-900002");

        var requestPayload =
            CreateValidQuotationRequest(customerId);

        requestPayload.Currency = Currency.USD;

        requestPayload.Lines[0].Description =
            "Updated porcelain";

        using var response = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/quotations/{quotationId}",
            requestPayload);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var quotation =
            await response.Content
                .ReadFromJsonAsync<QuotationResponse>();

        Assert.NotNull(quotation);

        Assert.Equal(
            quotationId,
            quotation.Id);

        Assert.Equal(
            "COT-900002",
            quotation.QuotationNumber);

        Assert.Equal(
            Currency.USD,
            quotation.Currency);

        var line = Assert.Single(
            quotation.Lines);

        Assert.Equal(
            "Updated porcelain",
            line.Description);

        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        var persistedQuotation =
            await dbContext.Quotations
                .AsNoTracking()
                .SingleAsync(
                    item => item.Id == quotationId);

        Assert.Equal(
            "COT-900002",
            persistedQuotation.QuotationNumber);
    }

    [Fact]
    public async Task UpdateQuotation_WhenQuotationDoesNotExist_ReturnsNotFound()
    {
        var customerId = await SeedCustomerAsync();

        var requestPayload =
            CreateValidQuotationRequest(customerId);

        using var response = await SendWithCsrfAsync(
            HttpMethod.Put,
            "/api/quotations/999999",
            requestPayload);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendWithCsrfAsync(
        HttpMethod method,
        string path,
        QuotationUpsertRequest requestPayload)
    {
        var csrfToken = await GetCsrfTokenAsync();

        using var request = new HttpRequestMessage(
            method,
            path)
        {
            Content = JsonContent.Create(requestPayload)
        };

        request.Headers.Add(
            "X-CSRF-TOKEN",
            csrfToken);

        return await client.SendAsync(request);
    }

    private async Task<string> GetCsrfTokenAsync()
    {
        using var response =
            await client.GetAsync("/api/auth/csrf");

        response.EnsureSuccessStatusCode();

        var payload =
            await response.Content
                .ReadFromJsonAsync<CsrfTokenResponse>();

        Assert.NotNull(payload);

        return payload.RequestToken;
    }

    private async Task<int> SeedCustomerAsync()
    {
        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        var customer = new Customer
        {
            Name = "Quotation Endpoint Customer",
            IdentificationType =
                IdentificationType.LegalEntity,
            IdentificationNumber = "3101999999",
            Email = "quotation-endpoint@example.com",
            PhoneNumber = "88888888",
            ProvinceCode = "4",
            CantonCode = "01",
            DistrictCode = "01",
            OtherSigns = "Quotation endpoint test address",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        return customer.Id;
    }

    private async Task<int> SeedQuotationAsync(
        int customerId,
        string quotationNumber)
    {
        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        var quotation = new Quotation
        {
            QuotationNumber = quotationNumber,
            CustomerId = customerId,
            Currency = Currency.CRC,
            IssuedAtUtc = DateTime.UtcNow,
            ValidUntilUtc = DateTime.UtcNow.AddDays(30),
            CreatedAtUtc = DateTime.UtcNow,
            Lines =
            [
                new QuotationLine
                {
                    CabysCode = "1234567890123",
                    Description = "Porcelanato 60x120",
                    Quantity = 1m,
                    UnitPrice = 15000m,
                    DiscountValue = 0m,
                    TaxRate = 13m
                }
            ]
        };

        dbContext.Quotations.Add(quotation);
        await dbContext.SaveChangesAsync();

        return quotation.Id;
    }

    private static QuotationUpsertRequest
        CreateValidQuotationRequest(
            int customerId)
    {
        return new QuotationUpsertRequest
        {
            CustomerId = customerId,
            Currency = Currency.CRC,
            ValidUntilUtc =
                DateTime.UtcNow.AddDays(30),

            Lines =
            [
                new QuotationLineRequest
                {
                    CabysCode = "1234567890123",
                    Description = "Porcelanato 60x120",
                    Quantity = 2m,
                    UnitPrice = 15000m,
                    DiscountType =
                        DiscountType.Percentage,
                    DiscountValue = 10m,
                    TaxRate = 13m
                }
            ],

            Charges =
            [
                new QuotationChargeRequest
                {
                    Type =
                        QuotationChargeType.Transport,
                    Description =
                        "Delivery to project",
                    Amount = 25000m,
                    TaxRate = 13m
                }
            ]
        };
    }
}