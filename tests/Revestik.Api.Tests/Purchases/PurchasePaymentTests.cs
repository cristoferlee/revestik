using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Purchases;
using Revestik.Shared.Purchases;
using Revestik.Shared.Sales;

namespace Revestik.Api.Tests.Purchases;

public sealed class PurchasePaymentTests
{
    [Fact]
    public async Task CreateCreditPurchase_WithAdvance_DerivesBalanceAndDueDate()
    {
        await using var db = CreateDb();
        var fixture = await SeedCatalogAsync(db);
        var service = new PurchaseService(db);

        var purchase = await service.CreateAsync(
            new PurchaseCreateRequest
            {
                SupplierId = fixture.SupplierId,
                PurchaseDate = new DateOnly(2026, 10, 1),
                Currency = PurchaseCurrency.CRC,
                PaymentType = PurchasePaymentType.Credit,
                CreditTermDays = 30,
                InitialPayment = new PurchasePaymentRequest
                {
                    Amount = 2500m,
                    PaymentMethod = PaymentMethod.BankTransfer,
                    PaidAtUtc = DateTime.UtcNow
                },
                Lines =
                [
                    new PurchaseLineRequest
                    {
                        ProductId = fixture.ProductId,
                        Quantity = 10m,
                        UnitCost = 1000m
                    }
                ]
            },
            fixture.UserId,
            CancellationToken.None);

        Assert.Equal(10000m, purchase.Total);
        Assert.Equal(2500m, purchase.PaidTotal);
        Assert.Equal(7500m, purchase.OutstandingAmount);
        Assert.Equal(
            PurchaseBalanceStatus.PartiallyPaid,
            purchase.BalanceStatus);
        Assert.Equal(new DateOnly(2026, 10, 31), purchase.DueDate);
        Assert.Single(purchase.Payments);
    }

    [Fact]
    public async Task CreateCashPurchase_WhenNotFullyPaid_Throws()
    {
        await using var db = CreateDb();
        var fixture = await SeedCatalogAsync(db);
        var service = new PurchaseService(db);

        await Assert.ThrowsAsync<InvalidPurchaseOperationException>(
            () => service.CreateAsync(
                new PurchaseCreateRequest
                {
                    SupplierId = fixture.SupplierId,
                    PurchaseDate =
                        DateOnly.FromDateTime(DateTime.Today),
                    Currency = PurchaseCurrency.CRC,
                    PaymentType = PurchasePaymentType.Cash,
                    InitialPayment = new PurchasePaymentRequest
                    {
                        Amount = 500m,
                        PaymentMethod = PaymentMethod.Cash,
                        PaidAtUtc = DateTime.UtcNow
                    },
                    Lines =
                    [
                        new PurchaseLineRequest
                        {
                            ProductId = fixture.ProductId,
                            Quantity = 1m,
                            UnitCost = 1000m
                        }
                    ]
                },
                fixture.UserId,
                CancellationToken.None));
    }

    [Fact]
    public async Task RegisterPayment_WhenAmountExceedsBalance_Throws()
    {
        await using var db = CreateDb();
        var fixture = await SeedCatalogAsync(db);
        var service = new PurchaseService(db);

        var purchase = await CreateCreditPurchaseAsync(
            service,
            fixture,
            initialPayment: 2000m);

        await Assert.ThrowsAsync<InvalidPurchaseOperationException>(
            () => service.RegisterPaymentAsync(
                purchase.Id,
                new PurchasePaymentRequest
                {
                    Amount = 8000.01m,
                    PaymentMethod = PaymentMethod.Sinpe,
                    PaidAtUtc = DateTime.UtcNow
                },
                fixture.UserId,
                CancellationToken.None));
    }

    [Fact]
    public async Task RegisterPayment_CompletesBalance()
    {
        await using var db = CreateDb();
        var fixture = await SeedCatalogAsync(db);
        var service = new PurchaseService(db);

        var purchase = await CreateCreditPurchaseAsync(
            service,
            fixture,
            initialPayment: 2000m);

        var result = await service.RegisterPaymentAsync(
            purchase.Id,
            new PurchasePaymentRequest
            {
                Amount = 8000m,
                PaymentMethod = PaymentMethod.BankTransfer,
                PaidAtUtc = DateTime.UtcNow
            },
            fixture.UserId,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(10000m, result.PaidTotal);
        Assert.Equal(0m, result.OutstandingAmount);
        Assert.Equal(
            PurchaseBalanceStatus.Paid,
            result.BalanceStatus);
    }

    [Fact]
    public async Task VoidPayment_PreservesHistoryAndReopensBalance()
    {
        await using var db = CreateDb();
        var fixture = await SeedCatalogAsync(db);
        var service = new PurchaseService(db);

        var purchase = await CreateCreditPurchaseAsync(
            service,
            fixture,
            initialPayment: 2000m);

        var paymentId = Assert.Single(purchase.Payments).Id;

        var result = await service.VoidPaymentAsync(
            purchase.Id,
            paymentId,
            new VoidPurchasePaymentRequest
            {
                Reason = "Monto registrado incorrectamente."
            },
            fixture.UserId,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0m, result.PaidTotal);
        Assert.Equal(10000m, result.OutstandingAmount);

        var payment = Assert.Single(result.Payments);
        Assert.Equal(
            PurchasePaymentStatus.Voided,
            payment.Status);
        Assert.NotNull(payment.VoidedAtUtc);
        Assert.Equal(
            "Monto registrado incorrectamente.",
            payment.VoidReason);
    }

    [Fact]
    public async Task UsdPayment_RequiresPaymentDateExchangeRate()
    {
        await using var db = CreateDb();
        var fixture = await SeedCatalogAsync(db);
        var service = new PurchaseService(db);

        var purchase = await service.CreateAsync(
            new PurchaseCreateRequest
            {
                SupplierId = fixture.SupplierId,
                PurchaseDate =
                    DateOnly.FromDateTime(DateTime.Today),
                Currency = PurchaseCurrency.USD,
                ExchangeRate = 505m,
                PaymentType = PurchasePaymentType.Credit,
                CreditTermDays = 30,
                Lines =
                [
                    new PurchaseLineRequest
                    {
                        ProductId = fixture.ProductId,
                        Quantity = 1m,
                        UnitCost = 100m
                    }
                ]
            },
            fixture.UserId,
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidPurchaseOperationException>(
            () => service.RegisterPaymentAsync(
                purchase.Id,
                new PurchasePaymentRequest
                {
                    Amount = 10m,
                    PaymentMethod = PaymentMethod.BankTransfer,
                    PaidAtUtc = DateTime.UtcNow
                },
                fixture.UserId,
                CancellationToken.None));
    }

    private static async Task<PurchaseResponse>
        CreateCreditPurchaseAsync(
            PurchaseService service,
            Fixture fixture,
            decimal initialPayment)
    {
        return await service.CreateAsync(
            new PurchaseCreateRequest
            {
                SupplierId = fixture.SupplierId,
                PurchaseDate =
                    DateOnly.FromDateTime(DateTime.Today),
                Currency = PurchaseCurrency.CRC,
                PaymentType = PurchasePaymentType.Credit,
                CreditTermDays = 30,
                InitialPayment = initialPayment > 0m
                    ? new PurchasePaymentRequest
                    {
                        Amount = initialPayment,
                        PaymentMethod = PaymentMethod.Cash,
                        PaidAtUtc = DateTime.UtcNow
                    }
                    : null,
                Lines =
                [
                    new PurchaseLineRequest
                    {
                        ProductId = fixture.ProductId,
                        Quantity = 10m,
                        UnitCost = 1000m
                    }
                ]
            },
            fixture.UserId,
            CancellationToken.None);
    }

    private static RevestikDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase(
                $"PurchasePayment-{Guid.NewGuid()}")
            .Options);

    private static async Task<Fixture> SeedCatalogAsync(
        RevestikDbContext db)
    {
        var userId = Guid.NewGuid().ToString();

        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@example.com",
            Email = $"{userId}@example.com",
            EmailConfirmed = true,
            DisplayName = "Purchase Payment User",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var supplier = new Supplier
        {
            Name = $"Supplier {Guid.NewGuid():N}",
            ContactName = "Contact",
            PhoneNumber = "88888888",
            Email = $"{Guid.NewGuid():N}@example.com",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var category = new ProductCategory
        {
            Name = $"Category {Guid.NewGuid():N}",
            CreatedAtUtc = DateTime.UtcNow
        };

        var unit = new UnitOfMeasure
        {
            Name = $"Unit {Guid.NewGuid():N}",
            Symbol = $"u{Random.Shared.Next(1000, 9999)}",
            RequiresWholeQuantity = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.AddRange(user, supplier, category, unit);
        await db.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            Name = "Product",
            Description = "Product",
            CabysCode = "1234567890123",
            InventoryUnitId = unit.Id,
            CommercialUnitId = unit.Id,
            CommercialUnitsPerInventoryUnit = 1m,
            RequiresWholeInventoryUnits = true,
            SalePrice = 2000m,
            CurrentCost = 1000m,
            TaxRate = 13m,
            StockQuantity = 0m,
            MinimumStock = 0m,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        return new Fixture(
            userId,
            supplier.Id,
            product.Id);
    }

    private sealed record Fixture(
        string UserId,
        int SupplierId,
        int ProductId);
}