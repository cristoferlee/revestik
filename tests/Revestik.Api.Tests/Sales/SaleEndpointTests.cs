using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Authentication;
using Revestik.Shared.Customers;
using Revestik.Shared.Sales;

namespace Revestik.Api.Tests.Sales;

public sealed class SaleEndpointTests(
    SqlServerIntegrationTestFixture sqlServerFixture)
    : IClassFixture<SqlServerIntegrationTestFixture>, IAsyncLifetime
{
    private const string TestUserId =
        "sale-endpoint-test-user";

    private RevestikWebApplicationFactory factory = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        await dbContext.SalePayments.ExecuteDeleteAsync();
        await dbContext.SaleCharges.ExecuteDeleteAsync();
        await dbContext.SaleLines.ExecuteDeleteAsync();
        await dbContext.Sales.ExecuteDeleteAsync();

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
                        "sale-endpoint-test@example.com",
                    NormalizedUserName =
                        "SALE-ENDPOINT-TEST@EXAMPLE.COM",
                    Email =
                        "sale-endpoint-test@example.com",
                    NormalizedEmail =
                        "SALE-ENDPOINT-TEST@EXAMPLE.COM",
                    EmailConfirmed = true,
                    DisplayName = "Sale Endpoint Test User",
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
        }
        else
        {
            existingUser.IsActive = true;
            existingUser.DisplayName =
                "Sale Endpoint Test User";
        }

        await dbContext.SaveChangesAsync();

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
    public async Task CreateSale_WithValidRequest_ReturnsCreatedDraft()
    {
        var customerId = await SeedCustomerAsync();

        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/sales",
            CreateValidSaleRequest(customerId));

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var sale =
            await response.Content.ReadFromJsonAsync<SaleResponse>();

        Assert.NotNull(sale);
        Assert.True(sale.Id > 0);
        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Equal(string.Empty, sale.SaleNumber);
        Assert.Equal(customerId, sale.CustomerId);
        Assert.Equal(TestUserId, sale.CreatedByUserId);
    }

    [Fact]
    public async Task IssueSale_AssignsVenAndIssuedState()
    {
        var customerId = await SeedCustomerAsync();

        var createResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/sales",
            CreateValidSaleRequest(customerId));

        var created =
            await createResponse.Content.ReadFromJsonAsync<SaleResponse>();

        Assert.NotNull(created);

        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/sales/{created.Id}/issue",
            CreateValidSaleRequest(customerId));

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var issued =
            await response.Content.ReadFromJsonAsync<SaleResponse>();

        Assert.NotNull(issued);
        Assert.Equal(SaleStatus.Issued, issued.Status);
        Assert.StartsWith("VEN-", issued.SaleNumber);
        Assert.NotNull(issued.IssuedAtUtc);
    }

    [Fact]
    public async Task RegisterPayment_UpdatesBalance()
    {
        var customerId = await SeedCustomerAsync();

        var createResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/sales",
            CreateValidSaleRequest(customerId));

        var created =
            await createResponse.Content.ReadFromJsonAsync<SaleResponse>();

        Assert.NotNull(created);

        var issueResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/sales/{created.Id}/issue",
            CreateValidSaleRequest(customerId));

        var issued =
            await issueResponse.Content.ReadFromJsonAsync<SaleResponse>();

        Assert.NotNull(issued);

        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/sales/{issued.Id}/payments",
            new SalePaymentRequest
            {
                Amount = 5000m,
                PaymentMethod = PaymentMethod.Sinpe,
                PaidAtUtc = DateTime.UtcNow
            });

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var sale =
            await response.Content.ReadFromJsonAsync<SaleResponse>();

        Assert.NotNull(sale);
        Assert.Equal(5000m, sale.PaidTotal);
        Assert.Equal(
            SaleBalanceStatus.PartiallyPaid,
            sale.BalanceStatus);
    }


    [Fact]
    public async Task DownloadSalePdf_WhenIssued_ReturnsPdf()
    {
        var customerId = await SeedCustomerAsync();

        var createResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/sales",
            CreateValidSaleRequest(customerId));

        var created =
            await createResponse.Content
                .ReadFromJsonAsync<SaleResponse>();

        Assert.NotNull(created);

        var issueResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/sales/{created.Id}/issue",
            CreateValidSaleRequest(customerId));

        var issued =
            await issueResponse.Content
                .ReadFromJsonAsync<SaleResponse>();

        Assert.NotNull(issued);

        using var response = await client.GetAsync(
            $"/api/sales/{issued.Id}/pdf");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.Equal(
            "application/pdf",
            response.Content.Headers.ContentType?.MediaType);

        Assert.Equal(
            $"{issued.SaleNumber}.pdf",
            response.Content.Headers.ContentDisposition?.FileNameStar ??
            response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));

        var bytes =
            await response.Content.ReadAsByteArrayAsync();

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task DownloadSalePdf_WhenDraft_ReturnsBadRequest()
    {
        var customerId = await SeedCustomerAsync();

        var createResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/sales",
            CreateValidSaleRequest(customerId));

        var created =
            await createResponse.Content
                .ReadFromJsonAsync<SaleResponse>();

        Assert.NotNull(created);

        using var response = await client.GetAsync(
            $"/api/sales/{created.Id}/pdf");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task GetSalesSummary_ReturnsOk()
    {
        using var response =
            await client.GetAsync("/api/sales/summary");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task SalesEndpoint_WhenRoleIsWarehouse_ReturnsForbidden()
    {
        using var warehouseClient = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true
            });

        warehouseClient.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.UserHeaderName,
            TestUserId);

        warehouseClient.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.RoleHeaderName,
            Revestik.Api.Authorization.RoleNames.Warehouse);

        using var response =
            await warehouseClient.GetAsync("/api/sales");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendWithCsrfAsync<T>(
        HttpMethod method,
        string path,
        T payload)
    {
        var csrfToken = await GetCsrfTokenAsync();

        using var request =
            new HttpRequestMessage(method, path)
            {
                Content = JsonContent.Create(payload)
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
            Name = "Sale Endpoint Customer",
            IdentificationType =
                IdentificationType.LegalEntity,
            IdentificationNumber =
                $"3101{Random.Shared.Next(100000, 999999)}",
            Email = "sale-endpoint@example.com",
            PhoneNumber = "88888888",
            ProvinceCode = "4",
            CantonCode = "01",
            DistrictCode = "01",
            OtherSigns = "Sale endpoint test address",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        return customer.Id;
    }

    private static SaleUpsertRequest
        CreateValidSaleRequest(int customerId)
    {
        return new SaleUpsertRequest
        {
            CustomerId = customerId,
            Currency = Currency.CRC,
            Observations = string.Empty,
            Lines =
            [
                new SaleLineRequest
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
            ]
        };
    }
}