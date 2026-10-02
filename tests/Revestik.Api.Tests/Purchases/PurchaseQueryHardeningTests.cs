using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Purchases;
using Revestik.Shared.Purchases;
using Revestik.Shared.Sales;

namespace Revestik.Api.Tests.Purchases;

public sealed class PurchaseQueryHardeningTests
{
    [Fact]
    public async Task VoidedPayment_DoesNotReduceOutstandingBalance()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db);

        var service = new PurchaseQueryService(db);

        var page = await service.GetPageAsync(
            new PurchaseListRequest
            {
                SupplierId = fixture.SupplierId
            },
            CancellationToken.None);

        var purchase = Assert.Single(page.Items);

        Assert.Equal(10000m, purchase.Total);
        Assert.Equal(2000m, purchase.PaidTotal);
        Assert.Equal(8000m, purchase.OutstandingAmount);
    }

    [Fact]
    public async Task PaidPurchase_DoesNotAppearInDueAlerts()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db);

        var purchase = await db.Purchases
            .Include(item => item.Lines)
            .SingleAsync(item =>
                item.SupplierId == fixture.SupplierId);

        purchase.Payments.Add(
            new PurchasePayment
            {
                Amount = 8000m,
                PaymentMethod = PaymentMethod.BankTransfer,
                PaidAtUtc = DateTime.UtcNow,
                Status = PurchasePaymentStatus.Active,
                CreatedByUserId = fixture.UserId,
                CreatedAtUtc = DateTime.UtcNow
            });

        await db.SaveChangesAsync();

        var service = new PurchaseQueryService(db);

        var alerts = await service.GetDueAlertsAsync(
            new PurchaseAlertRequest
            {
                ShortWindowDays = 3,
                LongWindowDays = 7
            },
            CancellationToken.None);

        Assert.Empty(alerts.Items);
    }

    private static RevestikDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase(
                $"PurchaseHardening-{Guid.NewGuid()}")
            .Options);

    private static async Task<Fixture> SeedAsync(
        RevestikDbContext db)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "hardening@example.com",
            Email = "hardening@example.com",
            EmailConfirmed = true,
            DisplayName = "Hardening User",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var supplier = new Supplier
        {
            Name = $"Supplier {Guid.NewGuid():N}",
            ContactName = "Contact",
            PhoneNumber = "88888888",
            Email = "hardening-supplier@example.com",
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
            CreatedAtUtc = DateTime.UtcNow
        };

        db.AddRange(
            user,
            supplier,
            category,
            unit);

        await db.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            Name = "Hardening Product",
            Description = "Hardening Product",
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

        var today =
            DateOnly.FromDateTime(DateTime.UtcNow);

        var purchase = new Purchase
        {
            SupplierId = supplier.Id,
            PurchaseDate = today.AddDays(-29),
            Currency = PurchaseCurrency.CRC,
            PaymentType = PurchasePaymentType.Credit,
            CreditTermDays = 30,
            DueDate = today.AddDays(1),
            Notes = string.Empty,
            CreatedByUserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow,
            Lines =
            [
                new PurchaseLine
                {
                    ProductId = product.Id,
                    Quantity = 10m,
                    UnitCost = 1000m
                }
            ],
            Payments =
            [
                new PurchasePayment
                {
                    Amount = 2000m,
                    PaymentMethod = PaymentMethod.Cash,
                    PaidAtUtc = DateTime.UtcNow,
                    Status = PurchasePaymentStatus.Active,
                    CreatedByUserId = user.Id,
                    CreatedAtUtc = DateTime.UtcNow
                },
                new PurchasePayment
                {
                    Amount = 3000m,
                    PaymentMethod = PaymentMethod.BankTransfer,
                    PaidAtUtc = DateTime.UtcNow,
                    Status = PurchasePaymentStatus.Voided,
                    CreatedByUserId = user.Id,
                    CreatedAtUtc = DateTime.UtcNow,
                    VoidedByUserId = user.Id,
                    VoidedAtUtc = DateTime.UtcNow,
                    VoidReason = "Prueba de anulación."
                }
            ]
        };

        db.Purchases.Add(purchase);
        await db.SaveChangesAsync();

        return new Fixture(
            user.Id,
            supplier.Id);
    }

    private sealed record Fixture(
        string UserId,
        int SupplierId);
}