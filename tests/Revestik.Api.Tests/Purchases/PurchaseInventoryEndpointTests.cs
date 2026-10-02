using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Authorization;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Authentication;
using Revestik.Shared.Inventory;
using Revestik.Shared.Purchases;
using Revestik.Shared.Sales;

namespace Revestik.Api.Tests.Purchases;

public sealed class PurchaseInventoryEndpointTests(
    SqlServerIntegrationTestFixture sqlServerFixture)
    : IClassFixture<SqlServerIntegrationTestFixture>,
      IAsyncLifetime
{
    private const string TestUserId =
        "purchase-endpoint-test-user";

    private RevestikWebApplicationFactory factory = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        await dbContext.InventoryCostConsumptions
            .ExecuteDeleteAsync();
        await dbContext.InventoryCostLayers
            .ExecuteDeleteAsync();
        await dbContext.InventoryMovements
            .ExecuteDeleteAsync();
        await dbContext.PurchasePayments
            .ExecuteDeleteAsync();
        await dbContext.PurchaseLines
            .ExecuteDeleteAsync();
        await dbContext.Purchases
            .ExecuteDeleteAsync();
        await dbContext.Products
            .ExecuteDeleteAsync();
        await dbContext.ProductCategories
            .ExecuteDeleteAsync();
        await dbContext.UnitsOfMeasure
            .ExecuteDeleteAsync();
        await dbContext.Suppliers
            .ExecuteDeleteAsync();

        var existingUser =
            await dbContext.Users
                .SingleOrDefaultAsync(
                    user => user.Id == TestUserId);

        if (existingUser is null)
        {
            dbContext.Users.Add(
                new ApplicationUser
                {
                    Id = TestUserId,
                    UserName =
                        "purchase-endpoint@example.com",
                    NormalizedUserName =
                        "PURCHASE-ENDPOINT@EXAMPLE.COM",
                    Email =
                        "purchase-endpoint@example.com",
                    NormalizedEmail =
                        "PURCHASE-ENDPOINT@EXAMPLE.COM",
                    EmailConfirmed = true,
                    DisplayName =
                        "Purchase Endpoint User",
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
        }
        else
        {
            existingUser.IsActive = true;
            existingUser.DisplayName =
                "Purchase Endpoint User";
        }

        await dbContext.SaveChangesAsync();

        factory = new RevestikWebApplicationFactory(
            "Development",
            sqlServerFixture.ConnectionString);

        client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress =
                    new Uri("https://localhost"),
                HandleCookies = true
            });

        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.UserHeaderName,
            TestUserId);

        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.RoleHeaderName,
            RoleNames.Warehouse);
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task CreatePurchase_Crc_CreatesPurchaseStockMovementAndFifoLayer()
    {
        var fixture = await SeedCatalogAsync(
            requiresWholeUnits: true);

        using var response = await SendWithCsrfAsync(
            new PurchaseCreateRequest
            {
                SupplierId = fixture.SupplierId,
                PurchaseDate =
                    DateOnly.FromDateTime(DateTime.Today),
                Currency = PurchaseCurrency.CRC,
                PaymentType = PurchasePaymentType.Cash,
                InitialPayment = new PurchasePaymentRequest
                {
                    Amount = 125000m,
                    PaymentMethod = PaymentMethod.BankTransfer,
                    PaidAtUtc = DateTime.UtcNow
                },
                Notes = "Compra para bodega.",
                Lines =
                [
                    new PurchaseLineRequest
                    {
                        ProductId = fixture.ProductId,
                        Quantity = 10m,
                        UnitCost = 12500m
                    }
                ]
            });

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var purchase =
            await response.Content
                .ReadFromJsonAsync<PurchaseResponse>();

        Assert.NotNull(purchase);
        Assert.Equal(125000m, purchase.Total);

        await using var verify =
            sqlServerFixture.CreateDbContext();

        var product = await verify.Products
            .AsNoTracking()
            .SingleAsync(item =>
                item.Id == fixture.ProductId);

        Assert.Equal(10m, product.StockQuantity);
        Assert.Equal(12500m, product.CurrentCost);

        var line = await verify.PurchaseLines
            .AsNoTracking()
            .SingleAsync(item =>
                item.PurchaseId == purchase.Id);

        var movement = await verify.InventoryMovements
            .AsNoTracking()
            .SingleAsync(item =>
                item.PurchaseLineId == line.Id);

        Assert.Equal(
            InventoryMovementType.Purchase,
            movement.Type);
        Assert.Equal(12500m, movement.UnitCost);

        var layer = await verify.InventoryCostLayers
            .AsNoTracking()
            .SingleAsync(item =>
                item.SourceMovementId == movement.Id);

        Assert.Equal(10m, layer.RemainingQuantity);
        Assert.Equal(12500m, layer.UnitCost);
    }

    [Fact]
    public async Task CreatePurchase_Usd_UsesHistoricalExchangeRateForInventoryCost()
    {
        var fixture = await SeedCatalogAsync(
            requiresWholeUnits: true);

        using var response = await SendWithCsrfAsync(
            new PurchaseCreateRequest
            {
                SupplierId = fixture.SupplierId,
                PurchaseDate =
                    DateOnly.FromDateTime(DateTime.Today),
                Currency = PurchaseCurrency.USD,
                ExchangeRate = 505.40m,
                PaymentType = PurchasePaymentType.Cash,
                InitialPayment = new PurchasePaymentRequest
                {
                    Amount = 20.50m,
                    PaymentMethod = PaymentMethod.InternationalTransfer,
                    PaidAtUtc = DateTime.UtcNow,
                    ExchangeRate = 505.40m
                },
                Lines =
                [
                    new PurchaseLineRequest
                    {
                        ProductId = fixture.ProductId,
                        Quantity = 2m,
                        UnitCost = 10.25m
                    }
                ]
            });

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        await using var verify =
            sqlServerFixture.CreateDbContext();

        var product = await verify.Products
            .AsNoTracking()
            .SingleAsync(item =>
                item.Id == fixture.ProductId);

        Assert.Equal(5180.35m, product.CurrentCost);

        var movement = await verify.InventoryMovements
            .AsNoTracking()
            .SingleAsync(item =>
                item.ProductId == fixture.ProductId &&
                item.Type ==
                    InventoryMovementType.Purchase);

        Assert.Equal(5180.35m, movement.UnitCost);
    }

    [Fact]
    public async Task CreatePurchase_WhenSecondLineFails_RollsBackEntireTransaction()
    {
        var first = await SeedCatalogAsync(
            requiresWholeUnits: true);

        var secondProductId =
            await SeedAdditionalProductAsync(
                first.CategoryId,
                first.UnitId,
                requiresWholeUnits: true);

        using var response = await SendWithCsrfAsync(
            new PurchaseCreateRequest
            {
                SupplierId = first.SupplierId,
                PurchaseDate =
                    DateOnly.FromDateTime(DateTime.Today),
                Currency = PurchaseCurrency.CRC,
                PaymentType = PurchasePaymentType.Cash,
                InitialPayment = new PurchasePaymentRequest
                {
                    Amount = 32000m,
                    PaymentMethod = PaymentMethod.Cash,
                    PaidAtUtc = DateTime.UtcNow
                },
                Lines =
                [
                    new PurchaseLineRequest
                    {
                        ProductId = first.ProductId,
                        Quantity = 2m,
                        UnitCost = 10000m
                    },
                    new PurchaseLineRequest
                    {
                        ProductId = secondProductId,
                        Quantity = 1.5m,
                        UnitCost = 8000m
                    }
                ]
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        await using var verify =
            sqlServerFixture.CreateDbContext();

        Assert.Empty(
            await verify.Purchases
                .AsNoTracking()
                .ToListAsync());

        Assert.Empty(
            await verify.PurchaseLines
                .AsNoTracking()
                .ToListAsync());

        Assert.Empty(
            await verify.InventoryMovements
                .AsNoTracking()
                .ToListAsync());

        Assert.Empty(
            await verify.InventoryCostLayers
                .AsNoTracking()
                .ToListAsync());

        var products = await verify.Products
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .ToListAsync();

        Assert.All(
            products,
            product =>
                Assert.Equal(
                    0m,
                    product.StockQuantity));
    }

    [Fact]
    public async Task CreatePurchase_WithDuplicateProduct_ReturnsBadRequest()
    {
        var fixture = await SeedCatalogAsync(
            requiresWholeUnits: true);

        using var response = await SendWithCsrfAsync(
            new PurchaseCreateRequest
            {
                SupplierId = fixture.SupplierId,
                PurchaseDate =
                    DateOnly.FromDateTime(DateTime.Today),
                Currency = PurchaseCurrency.CRC,
                PaymentType = PurchasePaymentType.Cash,
                InitialPayment = new PurchasePaymentRequest
                {
                    Amount = 20000m,
                    PaymentMethod = PaymentMethod.Cash,
                    PaidAtUtc = DateTime.UtcNow
                },
                Lines =
                [
                    new PurchaseLineRequest
                    {
                        ProductId = fixture.ProductId,
                        Quantity = 1m,
                        UnitCost = 10000m
                    },
                    new PurchaseLineRequest
                    {
                        ProductId = fixture.ProductId,
                        Quantity = 1m,
                        UnitCost = 10000m
                    }
                ]
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CreatePurchase_WhenAnonymous_ReturnsUnauthorized()
    {
        var fixture = await SeedCatalogAsync(
            requiresWholeUnits: true);

        using var anonymousClient =
            factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false,
                    BaseAddress =
                        new Uri("https://localhost"),
                    HandleCookies = true
                });

        using var response =
            await anonymousClient.PostAsJsonAsync(
                "/api/purchases",
                new PurchaseCreateRequest
                {
                    SupplierId = fixture.SupplierId,
                    PurchaseDate =
                        DateOnly.FromDateTime(
                            DateTime.Today),
                    Currency = PurchaseCurrency.CRC,
                    PaymentType = PurchasePaymentType.Cash,
                    InitialPayment = new PurchasePaymentRequest
                    {
                        Amount = 10000m,
                        PaymentMethod = PaymentMethod.Cash,
                        PaidAtUtc = DateTime.UtcNow
                    },
                    Lines =
                    [
                        new PurchaseLineRequest
                        {
                            ProductId =
                                fixture.ProductId,
                            Quantity = 1m,
                            UnitCost = 10000m
                        }
                    ]
                });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    private async Task<HttpResponseMessage>
        SendWithCsrfAsync(
            PurchaseCreateRequest request)
    {
        var csrfToken =
            await GetCsrfTokenAsync();

        using var message =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/purchases")
            {
                Content = JsonContent.Create(request)
            };

        message.Headers.Add(
            "X-CSRF-TOKEN",
            csrfToken);

        return await client.SendAsync(message);
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

    private async Task<CatalogFixture>
        SeedCatalogAsync(
            bool requiresWholeUnits)
    {
        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        var supplier = new Supplier
        {
            Name =
                $"Proveedor {Guid.NewGuid():N}",
            ContactName = "Contacto",
            PhoneNumber = "88888888",
            Email =
                $"supplier-{Guid.NewGuid():N}@example.com",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var category = new ProductCategory
        {
            Name =
                $"Categoría {Guid.NewGuid():N}",
            CreatedAtUtc = DateTime.UtcNow
        };

        var unit = new UnitOfMeasure
        {
            Name =
                $"Unidad {Guid.NewGuid():N}",
            Symbol =
                $"u{Random.Shared.Next(10000, 99999)}",
            RequiresWholeQuantity =
                requiresWholeUnits,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.AddRange(
            supplier,
            category,
            unit);

        await dbContext.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            Name = "Producto compra",
            Description = "Producto endpoint.",
            CabysCode = "1234567890123",
            InventoryUnitId = unit.Id,
            CommercialUnitId = unit.Id,
            CommercialUnitsPerInventoryUnit = 1m,
            RequiresWholeInventoryUnits =
                requiresWholeUnits,
            SalePrice = 15000m,
            CurrentCost = 9000m,
            TaxRate = 13m,
            StockQuantity = 0m,
            MinimumStock = 0m,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        return new CatalogFixture(
            supplier.Id,
            category.Id,
            unit.Id,
            product.Id);
    }

    private async Task<int>
        SeedAdditionalProductAsync(
            int categoryId,
            int unitId,
            bool requiresWholeUnits)
    {
        await using var dbContext =
            sqlServerFixture.CreateDbContext();

        var product = new Product
        {
            CategoryId = categoryId,
            Name = "Segundo producto compra",
            Description = "Segundo producto.",
            CabysCode = "9876543210123",
            InventoryUnitId = unitId,
            CommercialUnitId = unitId,
            CommercialUnitsPerInventoryUnit = 1m,
            RequiresWholeInventoryUnits =
                requiresWholeUnits,
            SalePrice = 12000m,
            CurrentCost = 8000m,
            TaxRate = 13m,
            StockQuantity = 0m,
            MinimumStock = 0m,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        return product.Id;
    }

    private sealed record CatalogFixture(
        int SupplierId,
        int CategoryId,
        int UnitId,
        int ProductId);
}