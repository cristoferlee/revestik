using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Authorization;
using Revestik.Api.Models;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Tests.Inventory;

public sealed class InventorySummaryEndpointTests(
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
            await dbContext.InventoryCostConsumptions.ExecuteDeleteAsync();
            await dbContext.InventoryCostLayers.ExecuteDeleteAsync();
            await dbContext.InventoryMovements.ExecuteDeleteAsync();
            await dbContext.Products.ExecuteDeleteAsync();
            await dbContext.ProductCategories.ExecuteDeleteAsync();
            await dbContext.UnitsOfMeasure.ExecuteDeleteAsync();

            var category = new ProductCategory
            {
                Name = "Porcelanatos",
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

            dbContext.Products.AddRange(
                CreateProduct(
                    category.Id,
                    unit.Id,
                    "Carrara",
                    10m,
                    5m),
                CreateProduct(
                    category.Id,
                    unit.Id,
                    "Calacatta",
                    3m,
                    5m),
                CreateProduct(
                    category.Id,
                    unit.Id,
                    "Agotado",
                    0m,
                    5m));

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
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task GetSummary_WhenAnonymous_ReturnsUnauthorized()
    {
        using var response =
            await client.GetAsync("/api/inventory/summary");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Administrator)]
    [InlineData(RoleNames.Accountant)]
    [InlineData(RoleNames.Sales)]
    [InlineData(RoleNames.Warehouse)]
    public async Task GetSummary_WithInventoryRole_ReturnsOk(
        string role)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/inventory/summary");

        request.Headers.Add(
            TestAuthenticationHandler.UserHeaderName,
            "inventory-summary-endpoint-user");

        request.Headers.TryAddWithoutValidation(
            TestAuthenticationHandler.RoleHeaderName,
            role);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = await response.Content
            .ReadFromJsonAsync<InventorySummaryResponse>();

        Assert.NotNull(summary);
        Assert.Equal(3, summary.TotalProducts);
        Assert.Equal(1, summary.InStockCount);
        Assert.Equal(1, summary.LowStockCount);
        Assert.Equal(1, summary.OutOfStockCount);
    }

    private static Product CreateProduct(
        int categoryId,
        int unitId,
        string name,
        decimal stock,
        decimal minimumStock)
    {
        return new Product
        {
            CategoryId = categoryId,
            Name = name,
            Description = name,
            CabysCode = "1234567890123",
            InventoryUnitId = unitId,
            CommercialUnitId = unitId,
            CommercialUnitsPerInventoryUnit = 1m,
            RequiresWholeInventoryUnits = true,
            SalePrice = 15000m,
            CurrentCost = 10000m,
            TaxRate = 13m,
            StockQuantity = stock,
            MinimumStock = minimumStock,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}