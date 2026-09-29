using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Inventory;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Authentication;
using Revestik.Shared.Customers;
using Revestik.Shared.Inventory;
using Revestik.Shared.Sales;

namespace Revestik.Api.Tests.Sales;

public sealed class SaleInventoryReversalEndpointTests(
    SqlServerIntegrationTestFixture sqlServerFixture)
    : IClassFixture<SqlServerIntegrationTestFixture>, IAsyncLifetime
{
    private const string TestUserId =
        "sale-inventory-reversal-endpoint-user";

    private RevestikWebApplicationFactory factory = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        await using var db =
            sqlServerFixture.CreateDbContext();

        await db.InventoryCostConsumptions.ExecuteDeleteAsync();
        await db.InventoryCostLayers.ExecuteDeleteAsync();
        await db.InventoryMovements.ExecuteDeleteAsync();

        await db.SalePayments.ExecuteDeleteAsync();
        await db.SaleCharges.ExecuteDeleteAsync();
        await db.SaleLines.ExecuteDeleteAsync();
        await db.Sales.ExecuteDeleteAsync();

        await db.Products.ExecuteDeleteAsync();
        await db.ProductCategories.ExecuteDeleteAsync();
        await db.UnitsOfMeasure.ExecuteDeleteAsync();
        await db.Customers.ExecuteDeleteAsync();

        var existingUser = await db.Users
            .SingleOrDefaultAsync(user => user.Id == TestUserId);

        if (existingUser is null)
        {
            db.Users.Add(
                new ApplicationUser
                {
                    Id = TestUserId,
                    UserName =
                        "sale-inventory-reversal@example.com",
                    NormalizedUserName =
                        "SALE-INVENTORY-REVERSAL@EXAMPLE.COM",
                    Email =
                        "sale-inventory-reversal@example.com",
                    NormalizedEmail =
                        "SALE-INVENTORY-REVERSAL@EXAMPLE.COM",
                    EmailConfirmed = true,
                    DisplayName =
                        "Sale Inventory Reversal User",
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
        }
        else
        {
            existingUser.IsActive = true;
            existingUser.DisplayName =
                "Sale Inventory Reversal User";
        }

        await db.SaveChangesAsync();

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
    public async Task VoidSale_WithInventory_RestoresStockAndCreatesReversal()
    {
        var fixture = await SeedIssuedInventorySaleAsync(
            initialStock: 100m,
            saleQuantity: 80m);

        Assert.Equal(20m, await GetProductStockAsync(fixture.ProductId));

        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/sales/{fixture.SaleId}/void",
            new VoidSaleRequest
            {
                Reason = "Cliente desistió de la compra."
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var sale =
            await response.Content.ReadFromJsonAsync<SaleResponse>();

        Assert.NotNull(sale);
        Assert.Equal(SaleStatus.Voided, sale.Status);
        Assert.Equal(100m, await GetProductStockAsync(fixture.ProductId));

        await using var db = sqlServerFixture.CreateDbContext();

        var original = await db.InventoryMovements
            .AsNoTracking()
            .SingleAsync(movement =>
                movement.SaleId == fixture.SaleId &&
                movement.Type == InventoryMovementType.Sale);

        var reversal = await db.InventoryMovements
            .AsNoTracking()
            .SingleAsync(movement =>
                movement.SaleId == fixture.SaleId &&
                movement.Type == InventoryMovementType.SaleReversal);

        Assert.Equal(
            original.Id,
            reversal.ReversesInventoryMovementId);
        Assert.Equal(80m, reversal.QuantityChange);

        var layer = await db.InventoryCostLayers
            .AsNoTracking()
            .SingleAsync(layer =>
                layer.ProductId == fixture.ProductId);

        Assert.Equal(100m, layer.RemainingQuantity);
    }

    [Fact]
    public async Task VoidSale_WithActivePayment_DoesNotChangeInventory()
    {
        var fixture = await SeedIssuedInventorySaleAsync(
            initialStock: 100m,
            saleQuantity: 80m);

        using var paymentResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/sales/{fixture.SaleId}/payments",
            new SalePaymentRequest
            {
                Amount = 1000m,
                PaymentMethod = PaymentMethod.Cash,
                PaidAtUtc = DateTime.UtcNow
            });

        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);

        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/sales/{fixture.SaleId}/void",
            new VoidSaleRequest
            {
                Reason = "Intento de anulación con pago."
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(20m, await GetProductStockAsync(fixture.ProductId));

        await using var db = sqlServerFixture.CreateDbContext();

        var sale = await db.Sales
            .AsNoTracking()
            .SingleAsync(item => item.Id == fixture.SaleId);

        Assert.Equal(SaleStatus.Issued, sale.Status);

        Assert.False(
            await db.InventoryMovements
                .AsNoTracking()
                .AnyAsync(movement =>
                    movement.SaleId == fixture.SaleId &&
                    movement.Type ==
                    InventoryMovementType.SaleReversal));
    }

    [Fact]
    public async Task CreateReplacement_RestoresInventoryAndCreatesDraftReplacement()
    {
        var fixture = await SeedIssuedInventorySaleAsync(
            initialStock: 100m,
            saleQuantity: 80m);

        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/sales/{fixture.SaleId}/replacement",
            new VoidSaleRequest
            {
                Reason = "Precio incorrecto."
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var replacement =
            await response.Content.ReadFromJsonAsync<SaleResponse>();

        Assert.NotNull(replacement);
        Assert.Equal(SaleStatus.Draft, replacement.Status);
        Assert.Equal(fixture.SaleId, replacement.ReplacesSaleId);
        Assert.Equal(100m, await GetProductStockAsync(fixture.ProductId));

        await using var db = sqlServerFixture.CreateDbContext();

        var original = await db.Sales
            .AsNoTracking()
            .SingleAsync(item => item.Id == fixture.SaleId);

        Assert.Equal(SaleStatus.Voided, original.Status);

        Assert.True(
            await db.InventoryMovements
                .AsNoTracking()
                .AnyAsync(movement =>
                    movement.SaleId == fixture.SaleId &&
                    movement.Type ==
                    InventoryMovementType.SaleReversal));
    }

    [Fact]
    public async Task VoidSale_WhenFifoRestorationIsInvalid_RollsBackSaleVoid()
    {
        var fixture = await SeedIssuedInventorySaleAsync(
            initialStock: 100m,
            saleQuantity: 80m);

        await CorruptRestorationCapacityAsync(
            fixture.ProductId);

        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/sales/{fixture.SaleId}/void",
            new VoidSaleRequest
            {
                Reason = "Debe hacer rollback."
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var db = sqlServerFixture.CreateDbContext();

        var sale = await db.Sales
            .AsNoTracking()
            .SingleAsync(item => item.Id == fixture.SaleId);

        Assert.Equal(SaleStatus.Issued, sale.Status);
        Assert.Null(sale.VoidedAtUtc);
        Assert.Equal(string.Empty, sale.VoidReason);

        var product = await db.Products
            .AsNoTracking()
            .SingleAsync(item => item.Id == fixture.ProductId);

        Assert.Equal(30m, product.StockQuantity);

        var layer = await db.InventoryCostLayers
            .AsNoTracking()
            .SingleAsync(item =>
                item.ProductId == fixture.ProductId);

        Assert.Equal(30m, layer.RemainingQuantity);

        Assert.False(
            await db.InventoryMovements
                .AsNoTracking()
                .AnyAsync(movement =>
                    movement.SaleId == fixture.SaleId &&
                    movement.Type ==
                    InventoryMovementType.SaleReversal));
    }

    [Fact]
    public async Task CreateReplacement_WhenFifoRestorationIsInvalid_RollsBackVoidAndDraft()
    {
        var fixture = await SeedIssuedInventorySaleAsync(
            initialStock: 100m,
            saleQuantity: 80m);

        await CorruptRestorationCapacityAsync(
            fixture.ProductId);

        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/sales/{fixture.SaleId}/replacement",
            new VoidSaleRequest
            {
                Reason = "Replacement debe hacer rollback."
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var db = sqlServerFixture.CreateDbContext();

        var original = await db.Sales
            .AsNoTracking()
            .SingleAsync(item => item.Id == fixture.SaleId);

        Assert.Equal(SaleStatus.Issued, original.Status);
        Assert.Null(original.VoidedAtUtc);
        Assert.Equal(string.Empty, original.VoidReason);

        Assert.False(
            await db.Sales
                .AsNoTracking()
                .AnyAsync(item =>
                    item.ReplacesSaleId == fixture.SaleId));

        Assert.False(
            await db.InventoryMovements
                .AsNoTracking()
                .AnyAsync(movement =>
                    movement.SaleId == fixture.SaleId &&
                    movement.Type ==
                    InventoryMovementType.SaleReversal));

        var product = await db.Products
            .AsNoTracking()
            .SingleAsync(item => item.Id == fixture.ProductId);

        Assert.Equal(30m, product.StockQuantity);
    }

    private async Task<(int ProductId, int SaleId)>
        SeedIssuedInventorySaleAsync(
            decimal initialStock,
            decimal saleQuantity)
    {
        var customerId = await SeedCustomerAsync();
        var product = await SeedInventoryProductAsync(
            initialStock);

        var request = CreateInventorySaleRequest(
            customerId,
            product,
            saleQuantity);

        using var createResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/sales",
            request);

        createResponse.EnsureSuccessStatusCode();

        var created =
            await createResponse.Content.ReadFromJsonAsync<SaleResponse>();

        Assert.NotNull(created);

        using var issueResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/sales/{created.Id}/issue",
            request);

        issueResponse.EnsureSuccessStatusCode();

        var issued =
            await issueResponse.Content.ReadFromJsonAsync<SaleResponse>();

        Assert.NotNull(issued);
        Assert.Equal(SaleStatus.Issued, issued.Status);

        return (product.Id, issued.Id);
    }

    private async Task<Product> SeedInventoryProductAsync(
        decimal initialStock)
    {
        await using var db =
            sqlServerFixture.CreateDbContext();

        var category = new ProductCategory
        {
            Name = $"Categoría reversal {Guid.NewGuid():N}",
            CreatedAtUtc = DateTime.UtcNow
        };

        var unit = new UnitOfMeasure
        {
            Name = $"Caja reversal {Guid.NewGuid():N}",
            Symbol = $"c{Guid.NewGuid():N}"[..10],
            CreatedAtUtc = DateTime.UtcNow
        };

        db.AddRange(category, unit);
        await db.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            Name = $"Producto reversal {Guid.NewGuid():N}",
            Description = "60x120, 1 caja.",
            CabysCode = "1234567890123",
            InventoryUnitId = unit.Id,
            CommercialUnitId = unit.Id,
            CommercialUnitsPerInventoryUnit = 1m,
            RequiresWholeInventoryUnits = true,
            SalePrice = 10000m,
            CurrentCost = 7000m,
            TaxRate = 13m,
            StockQuantity = 0m,
            MinimumStock = 0m,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        var inventoryService =
            new InventoryService(db);

        await inventoryService.RegisterInitialStockAsync(
            product.Id,
            new InitialStockRequest
            {
                Quantity = initialStock,
                Notes = "Inventario inicial endpoint reversal."
            },
            TestUserId,
            CancellationToken.None);

        return product;
    }

    private async Task<int> SeedCustomerAsync()
    {
        await using var db =
            sqlServerFixture.CreateDbContext();

        var customer = new Customer
        {
            Name = "Sale Inventory Reversal Customer",
            IdentificationType =
                IdentificationType.LegalEntity,
            IdentificationNumber =
                $"3101{Random.Shared.Next(100000, 999999)}",
            Email = "sale-reversal@example.com",
            PhoneNumber = "88888888",
            ProvinceCode = "4",
            CantonCode = "01",
            DistrictCode = "01",
            OtherSigns = "Integration test address",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        return customer.Id;
    }

    private static SaleUpsertRequest CreateInventorySaleRequest(
        int customerId,
        Product product,
        decimal quantity) =>
        new()
        {
            CustomerId = customerId,
            Currency = Currency.CRC,
            Observations = string.Empty,
            Lines =
            [
                new SaleLineRequest
                {
                    ProductId = product.Id,
                    CabysCode = product.CabysCode,
                    Description = product.Description,
                    Unit = "caja",
                    Quantity = quantity,
                    UnitPrice = product.SalePrice,
                    TaxRate = product.TaxRate
                }
            ]
        };

    private async Task CorruptRestorationCapacityAsync(
        int productId)
    {
        await using var db =
            sqlServerFixture.CreateDbContext();

        var product = await db.Products
            .SingleAsync(item => item.Id == productId);

        var layer = await db.InventoryCostLayers
            .SingleAsync(item =>
                item.ProductId == productId);

        product.StockQuantity = 30m;
        layer.RemainingQuantity = 30m;

        await db.SaveChangesAsync();
    }

    private async Task<decimal> GetProductStockAsync(
        int productId)
    {
        await using var db =
            sqlServerFixture.CreateDbContext();

        return await db.Products
            .AsNoTracking()
            .Where(item => item.Id == productId)
            .Select(item => item.StockQuantity)
            .SingleAsync();
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
}