using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Purchases;
using Revestik.Shared.Purchases;
using Revestik.Shared.Sales;

namespace Revestik.Api.Tests.Purchases;

public sealed class PurchaseQueryServiceTests
{
    [Fact]
    public async Task GetPageAsync_CalculatesDerivedBalances()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db);

        var service = new PurchaseQueryService(db);

        var result = await service.GetPageAsync(
            new PurchaseListRequest(),
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);

        var partial = Assert.Single(
            result.Items,
            item =>
                item.Id == fixture.PartialPurchaseId);

        Assert.Equal(10000m, partial.Total);
        Assert.Equal(2500m, partial.PaidTotal);
        Assert.Equal(7500m, partial.OutstandingAmount);
        Assert.Equal(
            PurchaseBalanceStatus.PartiallyPaid,
            partial.BalanceStatus);

        var paid = Assert.Single(
            result.Items,
            item =>
                item.Id == fixture.PaidPurchaseId);

        Assert.Equal(
            PurchaseBalanceStatus.Paid,
            paid.BalanceStatus);
        Assert.Equal(0m, paid.OutstandingAmount);
    }

    [Fact]
    public async Task GetPageAsync_FiltersByBalanceStatus()
    {
        await using var db = CreateDb();
        await SeedAsync(db);

        var service = new PurchaseQueryService(db);

        var result = await service.GetPageAsync(
            new PurchaseListRequest
            {
                BalanceStatus =
                    PurchaseBalanceStatus.PartiallyPaid
            },
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(
            PurchaseBalanceStatus.PartiallyPaid,
            result.Items[0].BalanceStatus);
    }

    [Fact]
    public async Task GetApSummaryAsync_ExcludesPaidPurchases()
    {
        await using var db = CreateDb();
        await SeedAsync(db);

        var service = new PurchaseQueryService(db);

        var result = await service.GetApSummaryAsync(
            CancellationToken.None);

        var crc = Assert.Single(result.Currencies);

        Assert.Equal(PurchaseCurrency.CRC, crc.Currency);
        Assert.Equal(1, crc.PurchaseCount);
        Assert.Equal(7500m, crc.OutstandingTotal);
    }

    [Fact]
    public async Task GetSupplierHistoryAsync_ReturnsOperationalTotals()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db);

        var service = new PurchaseQueryService(db);

        var result =
            await service.GetSupplierHistoryAsync(
                fixture.SupplierId,
                new SupplierPurchaseHistoryRequest(),
                CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.PurchaseCount);

        var crc = Assert.Single(result.Currencies);

        Assert.Equal(15000m, crc.OperationalPurchaseTotal);
        Assert.Equal(7500m, crc.OutstandingTotal);
        Assert.Equal(2, result.Purchases.TotalCount);
    }

    private static RevestikDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase(
                $"PurchaseQuery-{Guid.NewGuid()}")
            .Options);

    private static async Task<Fixture> SeedAsync(
        RevestikDbContext db)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "query@example.com",
            Email = "query@example.com",
            EmailConfirmed = true,
            DisplayName = "Query User",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var supplier = new Supplier
        {
            Name = $"Supplier {Guid.NewGuid():N}",
            ContactName = "Contact",
            PhoneNumber = "88888888",
            Email = "supplier@example.com",
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

        db.AddRange(user, supplier, category, unit);
        await db.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            Name = "Query Product",
            Description = "Query Product",
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

        var partial = new Purchase
        {
            SupplierId = supplier.Id,
            PurchaseDate =
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            Currency = PurchaseCurrency.CRC,
            PaymentType = PurchasePaymentType.Credit,
            CreditTermDays = 30,
            DueDate =
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(25)),
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
                    Amount = 2500m,
                    PaymentMethod = PaymentMethod.BankTransfer,
                    PaidAtUtc = DateTime.UtcNow,
                    Status = PurchasePaymentStatus.Active,
                    CreatedByUserId = user.Id,
                    CreatedAtUtc = DateTime.UtcNow
                }
            ]
        };

        var paid = new Purchase
        {
            SupplierId = supplier.Id,
            PurchaseDate =
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            Currency = PurchaseCurrency.CRC,
            PaymentType = PurchasePaymentType.Cash,
            Notes = string.Empty,
            CreatedByUserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow,
            Lines =
            [
                new PurchaseLine
                {
                    ProductId = product.Id,
                    Quantity = 5m,
                    UnitCost = 1000m
                }
            ],
            Payments =
            [
                new PurchasePayment
                {
                    Amount = 5000m,
                    PaymentMethod = PaymentMethod.Cash,
                    PaidAtUtc = DateTime.UtcNow,
                    Status = PurchasePaymentStatus.Active,
                    CreatedByUserId = user.Id,
                    CreatedAtUtc = DateTime.UtcNow
                }
            ]
        };

        db.Purchases.AddRange(partial, paid);
        await db.SaveChangesAsync();

        return new Fixture(
            supplier.Id,
            partial.Id,
            paid.Id);
    }

    private sealed record Fixture(
        int SupplierId,
        int PartialPurchaseId,
        int PaidPurchaseId);
}