using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Authentication;
using Revestik.Shared.Products;

namespace Revestik.Api.Tests.Products;

public sealed class ProductEndpointTests(
    SqlServerIntegrationTestFixture sqlServerFixture)
    : IClassFixture<SqlServerIntegrationTestFixture>, IAsyncLifetime
{
    private RevestikWebApplicationFactory factory = null!;
    private HttpClient client = null!;
    private int categoryId;
    private int inventoryUnitId;
    private int commercialUnitId;

    public async Task InitializeAsync()
    {
        await using (var dbContext =
            sqlServerFixture.CreateDbContext())
        {
            await dbContext.Products.ExecuteDeleteAsync();
            await dbContext.ProductCategories.ExecuteDeleteAsync();
            await dbContext.UnitsOfMeasure.ExecuteDeleteAsync();

            var category = new ProductCategory
            {
                Name = "Porcelanatos",
                CreatedAtUtc = DateTime.UtcNow
            };
            var inventoryUnit = new UnitOfMeasure
            {
                Name = "Caja",
                Symbol = "caja",
                CreatedAtUtc = DateTime.UtcNow
            };
            var commercialUnit = new UnitOfMeasure
            {
                Name = "Metro cuadrado",
                Symbol = "m²",
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.AddRange(
                category,
                inventoryUnit,
                commercialUnit);
            await dbContext.SaveChangesAsync();

            categoryId = category.Id;
            inventoryUnitId = inventoryUnit.Id;
            commercialUnitId = commercialUnit.Id;
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
            "product-endpoint-test-user");

    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task ProductLifecycle_ReturnsExpectedResponses()
    {
        var createResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/products",
            CreateRequest());

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content
            .ReadFromJsonAsync<ProductResponse>();

        Assert.NotNull(created);
        Assert.Equal(0m, created.StockQuantity);

        using var getResponse = await client.GetAsync(
            $"/api/products/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var updateRequest = CreateRequest();
        updateRequest.Name = "Carrara actualizado";

        using var updateResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/products/{created.Id}",
            updateRequest);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var deleteResponse = await SendWithCsrfAsync(
            HttpMethod.Delete,
            $"/api/products/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var activeListResponse = await client.GetAsync(
            "/api/products?page=1&pageSize=20&activityStatus=Active");
        var activeList = await activeListResponse.Content
            .ReadFromJsonAsync<
                Revestik.Shared.Common.PaginatedResponse<
                    ProductListItemResponse>>();

        Assert.NotNull(activeList);
        Assert.Empty(activeList.Items);

        using var reactivateResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/products/{created.Id}/reactivate");

        Assert.Equal(HttpStatusCode.NoContent, reactivateResponse.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_WithMissingCategory_ReturnsBadRequest()
    {
        var request = CreateRequest();
        request.CategoryId = int.MaxValue;

        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/products",
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendWithCsrfAsync(
        HttpMethod method,
        string path,
        ProductUpsertRequest? payload = null)
    {
        var csrfToken = await GetCsrfTokenAsync();

        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

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

    private ProductUpsertRequest CreateRequest()
    {
        return new ProductUpsertRequest
        {
            CategoryId = categoryId,
            Name = "Carrara Blanco 60x120",
            Description = "Porcelanato rectificado.",
            CabysCode = "1234567890123",
            InventoryUnitId = inventoryUnitId,
            CommercialUnitId = commercialUnitId,
            CommercialUnitsPerInventoryUnit = 1.44m,
            RequiresWholeInventoryUnits = true,
            SalePrice = 15000m,
            CurrentCost = 10000m,
            TaxRate = 13m,
            MinimumStock = 5m
        };
    }
}