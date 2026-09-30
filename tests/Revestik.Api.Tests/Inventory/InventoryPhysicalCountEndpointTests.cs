using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Inventory;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Authentication;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Tests.Inventory;

public sealed class InventoryPhysicalCountEndpointTests
    : IAsyncLifetime
{
    private const string TestUserId =
        "physical-count-endpoint-user";

    private RevestikWebApplicationFactory factory = null!;
    private HttpClient client = null!;
    private int productId;

    public async Task InitializeAsync()
    {
        factory = new RevestikWebApplicationFactory(
            "Development");

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

        await using var scope =
            factory.Services.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<RevestikDbContext>();

        dbContext.Users.Add(
            new ApplicationUser
            {
                Id = TestUserId,
                UserName = "physical-count-endpoint@example.com",
                NormalizedUserName =
                    "PHYSICAL-COUNT-ENDPOINT@EXAMPLE.COM",
                Email = "physical-count-endpoint@example.com",
                NormalizedEmail =
                    "PHYSICAL-COUNT-ENDPOINT@EXAMPLE.COM",
                EmailConfirmed = true,
                DisplayName = "Physical Count Endpoint User",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });

        var category = new ProductCategory
        {
            Name = "Physical Count Category",
            CreatedAtUtc = DateTime.UtcNow
        };

        var unit = new UnitOfMeasure
        {
            Name = "Caja",
            Symbol = "caja",
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.AddRange(category, unit);
        await dbContext.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            Name = "Physical Count Product",
            Description = "Endpoint fixture.",
            CabysCode = "1234567890123",
            InventoryUnitId = unit.Id,
            CommercialUnitId = unit.Id,
            CommercialUnitsPerInventoryUnit = 1m,
            RequiresWholeInventoryUnits = true,
            SalePrice = 15000m,
            CurrentCost = 10000m,
            TaxRate = 13m,
            StockQuantity = 0m,
            MinimumStock = 0m,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var inventoryService =
            scope.ServiceProvider
                .GetRequiredService<IInventoryService>();

        await inventoryService.RegisterInitialStockAsync(
            product.Id,
            new InitialStockRequest
            {
                Quantity = 10m,
                Notes = "Endpoint test initial stock."
            },
            TestUserId,
            CancellationToken.None);

        productId = product.Id;
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task PhysicalCount_FullEndpointFlow_CompletesAndAdjustsInventory()
    {
        using var createResponse =
            await SendWithCsrfAsync(
                HttpMethod.Post,
                "/api/inventory/physical-counts",
                new PhysicalCountCreateRequest
                {
                    Notes = "Endpoint flow."
                });

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var created =
            await createResponse.Content
                .ReadFromJsonAsync<PhysicalCountResponse>();

        Assert.NotNull(created);
        Assert.Equal(
            PhysicalCountStatus.Draft,
            created.Status);

        var line = Assert.Single(created.Lines);
        Assert.Equal(productId, line.ProductId);
        Assert.Equal(10m, line.ExpectedQuantity);
        Assert.True(line.RequiresWholeInventoryUnits);

        using var updateResponse =
            await SendWithCsrfAsync(
                HttpMethod.Put,
                $"/api/inventory/physical-counts/{created.Id}/lines",
                new PhysicalCountUpdateRequest
                {
                    Lines =
                    [
                        new PhysicalCountLineUpdateRequest
                        {
                            LineId = line.Id,
                            CountedQuantity = 8m
                        }
                    ]
                });

        Assert.Equal(
            HttpStatusCode.OK,
            updateResponse.StatusCode);

        using var completeResponse =
            await SendWithCsrfAsync(
                HttpMethod.Post,
                $"/api/inventory/physical-counts/{created.Id}/complete");

        Assert.Equal(
            HttpStatusCode.OK,
            completeResponse.StatusCode);

        var completed =
            await completeResponse.Content
                .ReadFromJsonAsync<PhysicalCountResponse>();

        Assert.NotNull(completed);
        Assert.Equal(
            PhysicalCountStatus.Completed,
            completed.Status);
        Assert.Equal(-2m, completed.Lines.Single().DifferenceQuantity);

        await using var scope =
            factory.Services.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<RevestikDbContext>();

        var product =
            await dbContext.Products
                .AsNoTracking()
                .SingleAsync(item =>
                    item.Id == productId);

        Assert.Equal(8m, product.StockQuantity);

        var movement =
            await dbContext.InventoryMovements
                .AsNoTracking()
                .SingleAsync(item =>
                    item.PhysicalCountId ==
                    created.Id);

        Assert.Equal(-2m, movement.QuantityChange);
        Assert.Equal(
            InventoryAdjustmentReason.PhysicalCount,
            movement.AdjustmentReason);
    }

    [Fact]
    public async Task GetPhysicalCount_WhenMissing_ReturnsNotFound()
    {
        using var response =
            await client.GetAsync(
                "/api/inventory/physical-counts/999999");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task UpdatePhysicalCount_WithNegativeQuantity_ReturnsBadRequest()
    {
        var created = await StartCountAsync();
        var line = Assert.Single(created.Lines);

        using var response =
            await SendWithCsrfAsync(
                HttpMethod.Put,
                $"/api/inventory/physical-counts/{created.Id}/lines",
                new PhysicalCountUpdateRequest
                {
                    Lines =
                    [
                        new PhysicalCountLineUpdateRequest
                        {
                            LineId = line.Id,
                            CountedQuantity = -1m
                        }
                    ]
                });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task StartPhysicalCount_WithoutCsrf_ReturnsBadRequest()
    {
        using var response =
            await client.PostAsJsonAsync(
                "/api/inventory/physical-counts",
                new PhysicalCountCreateRequest());

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task PhysicalCount_WithUnauthorizedRole_ReturnsForbidden()
    {
        using var unauthorizedClient =
            factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false,
                    BaseAddress =
                        new Uri("https://localhost"),
                    HandleCookies = true
                });

        unauthorizedClient.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.UserHeaderName,
            TestUserId);

        unauthorizedClient.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.RoleHeaderName,
            "Customer");

        using var response =
            await unauthorizedClient.GetAsync(
                "/api/inventory/physical-counts/1");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    private async Task<PhysicalCountResponse> StartCountAsync()
    {
        using var response =
            await SendWithCsrfAsync(
                HttpMethod.Post,
                "/api/inventory/physical-counts",
                new PhysicalCountCreateRequest());

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<PhysicalCountResponse>()
            ?? throw new InvalidOperationException(
                "Physical count response was empty.");
    }

    private async Task<HttpResponseMessage> SendWithCsrfAsync(
        HttpMethod method,
        string path,
        object? payload = null)
    {
        var csrfToken = await GetCsrfTokenAsync();

        using var request =
            new HttpRequestMessage(method, path);

        if (payload is not null)
        {
            request.Content =
                JsonContent.Create(payload);
        }

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
}
