using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Purchases;
using Revestik.Shared.Purchases;

namespace Revestik.Api.Tests.Purchases;

public sealed class PurchaseDueAlertTests
{
    [Fact]
    public async Task GetDueAlertsAsync_ClassifiesDueDates()
    {
        await using var db = CreateDb();

        var fixture = await SeedBaseAsync(db);

        var today =
            DateOnly.FromDateTime(DateTime.UtcNow);

        await AddCreditPurchaseAsync(
            db,
            fixture,
            today.AddDays(-2));

        await AddCreditPurchaseAsync(
            db,
            fixture,
            today);

        await AddCreditPurchaseAsync(
            db,
            fixture,
            today.AddDays(2));

        await AddCreditPurchaseAsync(
            db,
            fixture,
            today.AddDays(6));

        var service = new PurchaseQueryService(db);

        var result = await service.GetDueAlertsAsync(
            new PurchaseAlertRequest
            {
                ShortWindowDays = 3,
                LongWindowDays = 7
            },
            CancellationToken.None);

        Assert.Equal(4, result.Items.Count);

        Assert.Contains(
            result.Items,
            item =>
                item.AlertType ==
                PurchaseDueAlertType.Overdue);

        Assert.Contains(
            result.Items,
            item =>
                item.AlertType ==
                PurchaseDueAlertType.DueToday);

        Assert.Contains(
            result.Items,
            item =>
                item.AlertType ==
                PurchaseDueAlertType
                    .DueWithinShortWindow);

        Assert.Contains(
            result.Items,
            item =>
                item.AlertType ==
                PurchaseDueAlertType
                    .DueWithinLongWindow);
    }

    private static RevestikDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase(
                $"PurchaseAlerts-{Guid.NewGuid()}")
            .Options);

    private static async Task<Fixture> SeedBaseAsync(
        RevestikDbContext db)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "alerts@example.com",
            Email = "alerts@example.com",
            EmailConfirmed = true,
            DisplayName = "Alerts User",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var supplier = new Supplier
        {
            Name = $"Supplier {Guid.NewGuid():N}",
            ContactName = "Contact",
            PhoneNumber = "88888888",
            Email = "alerts-supplier@example.com",
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
            Name = "Alert Product",
            Description = "Alert Product",
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
            user.Id,
            supplier.Id,
            product.Id);
    }

    private static async Task AddCreditPurchaseAsync(
        RevestikDbContext db,
        Fixture fixture,
        DateOnly dueDate)
    {
        var purchaseDate = dueDate.AddDays(-30);

        db.Purchases.Add(
            new Purchase
            {
                SupplierId = fixture.SupplierId,
                PurchaseDate = purchaseDate,
                Currency = PurchaseCurrency.CRC,
                PaymentType = PurchasePaymentType.Credit,
                CreditTermDays = 30,
                DueDate = dueDate,
                Notes = string.Empty,
                CreatedByUserId = fixture.UserId,
                CreatedAtUtc = DateTime.UtcNow,
                Lines =
                [
                    new PurchaseLine
                    {
                        ProductId = fixture.ProductId,
                        Quantity = 1m,
                        UnitCost = 1000m
                    }
                ]
            });

        await db.SaveChangesAsync();
    }

    private sealed record Fixture(
        string UserId,
        int SupplierId,
        int ProductId);
}