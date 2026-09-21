using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Authentication;
using Revestik.Shared.Customers;
using Revestik.Shared.Quotations;

namespace Revestik.Api.Tests.Quotations;

public sealed class QuotationEndpointTests(
    SqlServerIntegrationTestFixture sqlServerFixture)
    : IClassFixture<SqlServerIntegrationTestFixture>, IAsyncLifetime
{
    private const string TestUserId =
        "quotation-endpoint-test-user";

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

            var existingUser =
                await dbContext.Users.SingleOrDefaultAsync(
                    user => user.Id == TestUserId);

            if (existingUser is null)
            {
                dbContext.Users.Add(
                    new ApplicationUser
                    {
                        Id = TestUserId,
                        UserName =
                            "quotation-endpoint-test@example.com",
                        NormalizedUserName =
                            "QUOTATION-ENDPOINT-TEST@EXAMPLE.COM",
                        Email =
                            "quotation-endpoint-test@example.com",
                        NormalizedEmail =
                            "QUOTATION-ENDPOINT-TEST@EXAMPLE.COM",
                        EmailConfirmed = true,
                        DisplayName =
                            "Quotation Endpoint Test User",
                        IsActive = true,
                        CreatedAtUtc = DateTime.UtcNow
                    });
            }
            else
            {
                existingUser.IsActive = true;
                existingUser.DisplayName =
                    "Quotation Endpoint Test User";
            }

            await dbContext.SaveChangesAsync();
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
            TestUserId);
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

        Assert.Equal(
            TestUserId,
            quotation.CreatedByUserId);

        Assert.Equal(
            "Quotation Endpoint Test User",
            quotation.CreatedByDisplayName);
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
            QuotationStatus.Draft,
            quotation.Status);

        Assert.Null(quotation.IssuedAtUtc);

        Assert.Equal(
            TestUserId,
            quotation.CreatedByUserId);

        Assert.Equal(
            "Quotation Endpoint Test User",
            quotation.CreatedByDisplayName);

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

        Assert.Equal(
            TestUserId,
            persistedQuotation.CreatedByUserId);

        Assert.Equal(
            QuotationStatus.Draft,
            persistedQuotation.Status);

        Assert.Null(
            persistedQuotation.IssuedAtUtc);
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
    public async Task CreateQuotation_WithEmptyCabys_ReturnsCreated()
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
            HttpStatusCode.Created,
            response.StatusCode);

        var quotation =
            await response.Content
                .ReadFromJsonAsync<QuotationResponse>();

        Assert.NotNull(quotation);

        var line = Assert.Single(
            quotation.Lines);

        Assert.Equal(
            string.Empty,
            line.CabysCode);

        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        var persistedLine =
            await dbContext.QuotationLines
                .AsNoTracking()
                .SingleAsync(
                    item =>
                        item.QuotationId ==
                        quotation.Id);

        Assert.Null(persistedLine.CabysCode);
    }

    [Fact]
    public async Task CreateQuotation_WithInvalidRequest_ReturnsBadRequest()
    {
        var customerId = await SeedCustomerAsync();

        var requestPayload =
            CreateValidQuotationRequest(customerId);

        requestPayload.Lines[0].Quantity = 1.234m;

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

        requestPayload.Observations =
            "Updated conditions";

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

        Assert.Equal(
            TestUserId,
            quotation.CreatedByUserId);

        Assert.Equal(
            "Updated conditions",
            quotation.Observations);

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

        Assert.Equal(
            TestUserId,
            persistedQuotation.CreatedByUserId);

        Assert.Equal(
            QuotationStatus.Draft,
            persistedQuotation.Status);

        Assert.Null(
            persistedQuotation.IssuedAtUtc);
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


    [Fact]
    public async Task IssueQuotation_WithValidRequest_ReturnsOkAndPersistsIssuedState()
    {
        var customerId = await SeedCustomerAsync();
        var quotationId = await SeedQuotationAsync(
            customerId,
            "COT-900003");

        var requestPayload = CreateValidQuotationRequest(customerId);
        requestPayload.Currency = Currency.USD;
        requestPayload.Observations = "Issued conditions";
        requestPayload.Lines[0].Description = "Issued porcelain";
        requestPayload.Charges[0].Type = QuotationChargeType.Installation;
        requestPayload.Charges[0].Description = "Installation";
        requestPayload.Charges[0].Amount = 30000m;

        var beforeIssue = DateTime.UtcNow;

        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/quotations/{quotationId}/issue",
            requestPayload);

        var afterIssue = DateTime.UtcNow;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var quotation = await response.Content
            .ReadFromJsonAsync<QuotationResponse>();

        Assert.NotNull(quotation);
        Assert.Equal(quotationId, quotation.Id);
        Assert.Equal("COT-900003", quotation.QuotationNumber);
        Assert.Equal(QuotationStatus.Issued, quotation.Status);
        Assert.NotNull(quotation.IssuedAtUtc);
        Assert.InRange(quotation.IssuedAtUtc.Value, beforeIssue, afterIssue);
        Assert.Equal(TestUserId, quotation.CreatedByUserId);
        Assert.Equal(Currency.USD, quotation.Currency);
        Assert.Equal("Issued conditions", quotation.Observations);

        var line = Assert.Single(quotation.Lines);
        Assert.Equal("Issued porcelain", line.Description);

        var charge = Assert.Single(quotation.Charges);
        Assert.Equal(QuotationChargeType.Installation, charge.Type);
        Assert.Equal("Installation", charge.Description);
        Assert.Equal(30000m, charge.Amount);

        await using var dbContext = sqlServerFixture.CreateDbContext();

        var persistedQuotation = await dbContext.Quotations
            .AsNoTracking()
            .SingleAsync(item => item.Id == quotationId);

        Assert.Equal(QuotationStatus.Issued, persistedQuotation.Status);
        Assert.NotNull(persistedQuotation.IssuedAtUtc);
        Assert.Equal("COT-900003", persistedQuotation.QuotationNumber);
        Assert.Equal(TestUserId, persistedQuotation.CreatedByUserId);
    }

    [Fact]
    public async Task IssueQuotation_WhenQuotationDoesNotExist_ReturnsNotFound()
    {
        var customerId = await SeedCustomerAsync();
        var requestPayload = CreateValidQuotationRequest(customerId);

        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/quotations/999999/issue",
            requestPayload);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task IssueQuotation_WithMissingCustomer_ReturnsBadRequestAndKeepsDraft()
    {
        var customerId = await SeedCustomerAsync();
        var quotationId = await SeedQuotationAsync(
            customerId,
            "COT-900004");

        var requestPayload = CreateValidQuotationRequest(999999);

        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/quotations/{quotationId}/issue",
            requestPayload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var dbContext = sqlServerFixture.CreateDbContext();

        var persistedQuotation = await dbContext.Quotations
            .AsNoTracking()
            .SingleAsync(item => item.Id == quotationId);

        Assert.Equal(QuotationStatus.Draft, persistedQuotation.Status);
        Assert.Null(persistedQuotation.IssuedAtUtc);
        Assert.Equal("COT-900004", persistedQuotation.QuotationNumber);
        Assert.Equal(TestUserId, persistedQuotation.CreatedByUserId);
    }

    [Fact]
    public async Task IssueQuotation_WithInvalidRequest_ReturnsBadRequestAndKeepsDraft()
    {
        var customerId = await SeedCustomerAsync();
        var quotationId = await SeedQuotationAsync(
            customerId,
            "COT-900005");

        var requestPayload = CreateValidQuotationRequest(customerId);
        requestPayload.Lines[0].Quantity = 1.234m;

        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/quotations/{quotationId}/issue",
            requestPayload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var dbContext = sqlServerFixture.CreateDbContext();

        var persistedQuotation = await dbContext.Quotations
            .AsNoTracking()
            .SingleAsync(item => item.Id == quotationId);

        Assert.Equal(QuotationStatus.Draft, persistedQuotation.Status);
        Assert.Null(persistedQuotation.IssuedAtUtc);
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
            CreatedByUserId = TestUserId,
            Currency = Currency.CRC,
            Status = QuotationStatus.Draft,
            IssuedAtUtc = null,
            ValidUntilUtc = DateTime.UtcNow.AddDays(30),
            Observations = string.Empty,
            CreatedAtUtc = DateTime.UtcNow,
            Lines =
            [
                new QuotationLine
                {
                    CabysCode = "1234567890123",
                    Description = "Porcelanato 60x120",
                    Unit = "m²",
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
            Observations = string.Empty,

            Lines =
            [
                new QuotationLineRequest
                {
                    CabysCode = "1234567890123",
                    Description = "Porcelanato 60x120",
                    Unit = "m²",
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
                    Amount = 25000m
                }
            ]
        };
    }
}