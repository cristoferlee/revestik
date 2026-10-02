using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Purchases;
using Revestik.Shared.Sales;

namespace Revestik.Api.Tests.Purchases;

public sealed class PurchasePaymentDataIntegrityIntegrationTests(
    SqlServerIntegrationTestFixture sqlServerFixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    [Fact]
    public async Task PurchasePayment_WithNonPositiveAmount_IsRejectedBySqlServer()
    {
        await using var db =
            sqlServerFixture.CreateDbContext();

        var fixture = await SeedPurchaseAsync(db);

        db.PurchasePayments.Add(
            new PurchasePayment
            {
                PurchaseId = fixture.PurchaseId,
                Amount = 0m,
                PaymentMethod = PaymentMethod.Cash,
                PaidAtUtc = DateTime.UtcNow,
                Status = PurchasePaymentStatus.Active,
                CreatedByUserId = fixture.UserId,
                CreatedAtUtc = DateTime.UtcNow
            });

        await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync());
    }

    private static async Task<Fixture> SeedPurchaseAsync(
        Revestik.Api.Data.RevestikDbContext db)
    {
        var userId = Guid.NewGuid().ToString();

        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@example.com",
            Email = $"{userId}@example.com",
            EmailConfirmed = true,
            DisplayName = "Purchase SQL User",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var supplier = new Supplier
        {
            Name = $"SQL Supplier {Guid.NewGuid():N}",
            ContactName = "Contact",
            PhoneNumber = "88888888",
            Email = $"{Guid.NewGuid():N}@example.com",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.AddRange(user, supplier);
        await db.SaveChangesAsync();

        var purchase = new Purchase
        {
            SupplierId = supplier.Id,
            PurchaseDate =
                DateOnly.FromDateTime(DateTime.Today),
            Currency = PurchaseCurrency.CRC,
            PaymentType = PurchasePaymentType.Credit,
            CreditTermDays = 30,
            DueDate =
                DateOnly.FromDateTime(DateTime.Today)
                    .AddDays(30),
            Notes = string.Empty,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Purchases.Add(purchase);
        await db.SaveChangesAsync();

        return new Fixture(userId, purchase.Id);
    }

    private sealed record Fixture(
        string UserId,
        int PurchaseId);
}