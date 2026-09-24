using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Sales;
using Revestik.Shared.Customers;
using Revestik.Shared.Sales;

namespace Revestik.Api.Tests.Sales;

public sealed class SalePaymentTests
{
    [Fact]
    public async Task RegisterPaymentAsync_AddsPartialPayment()
    {
        await using var dbContext = CreateDbContext();
        var (customer, user, sale) =
            await SeedIssuedSaleAsync(dbContext);

        var service = CreateService(dbContext);

        var paidAt = DateTime.UtcNow.AddDays(-1);

        var result = await service.RegisterPaymentAsync(
            sale.Id,
            new SalePaymentRequest
            {
                Amount = 10000m,
                PaymentMethod = PaymentMethod.Sinpe,
                PaidAtUtc = paidAt,
                Reference = "SINPE-001"
            },
            user.Id,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(10000m, result.PaidTotal);
        Assert.Equal(20000m, result.OutstandingAmount);
        Assert.Equal(
            SaleBalanceStatus.PartiallyPaid,
            result.BalanceStatus);
        Assert.Equal(
            paidAt.AddDays(7),
            result.NextPaymentDueAtUtc);

        var payment = Assert.Single(result.Payments);
        Assert.Equal(PaymentMethod.Sinpe, payment.PaymentMethod);
        Assert.Equal(SalePaymentStatus.Active, payment.Status);
    }

    [Fact]
    public async Task RegisterPaymentAsync_WhenPaymentCompletesBalance_MarksPaid()
    {
        await using var dbContext = CreateDbContext();
        var (_, user, sale) =
            await SeedIssuedSaleAsync(dbContext);

        var service = CreateService(dbContext);

        var result = await service.RegisterPaymentAsync(
            sale.Id,
            new SalePaymentRequest
            {
                Amount = 30000m,
                PaymentMethod = PaymentMethod.BankTransfer,
                PaidAtUtc = DateTime.UtcNow
            },
            user.Id,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(30000m, result.PaidTotal);
        Assert.Equal(0m, result.OutstandingAmount);
        Assert.Equal(
            SaleBalanceStatus.Paid,
            result.BalanceStatus);
        Assert.Null(result.NextPaymentDueAtUtc);
    }

    [Fact]
    public async Task RegisterPaymentAsync_AllowsMultiplePaymentMethods()
    {
        await using var dbContext = CreateDbContext();
        var (_, user, sale) =
            await SeedIssuedSaleAsync(dbContext);

        var service = CreateService(dbContext);

        await service.RegisterPaymentAsync(
            sale.Id,
            new SalePaymentRequest
            {
                Amount = 10000m,
                PaymentMethod = PaymentMethod.Cash,
                PaidAtUtc = DateTime.UtcNow.AddDays(-2)
            },
            user.Id,
            CancellationToken.None);

        var result = await service.RegisterPaymentAsync(
            sale.Id,
            new SalePaymentRequest
            {
                Amount = 5000m,
                PaymentMethod = PaymentMethod.Card,
                PaidAtUtc = DateTime.UtcNow.AddDays(-1)
            },
            user.Id,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(15000m, result.PaidTotal);
        Assert.Equal(15000m, result.OutstandingAmount);
        Assert.Equal(2, result.Payments.Count);
    }

    [Fact]
    public async Task RegisterPaymentAsync_WhenAmountExceedsBalance_Throws()
    {
        await using var dbContext = CreateDbContext();
        var (_, user, sale) =
            await SeedIssuedSaleAsync(dbContext);

        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RegisterPaymentAsync(
                sale.Id,
                new SalePaymentRequest
                {
                    Amount = 30000.01m,
                    PaymentMethod = PaymentMethod.Cash,
                    PaidAtUtc = DateTime.UtcNow
                },
                user.Id,
                CancellationToken.None));

        Assert.Empty(dbContext.SalePayments);
    }

    [Fact]
    public async Task RegisterPaymentAsync_WhenSaleIsDraft_Throws()
    {
        await using var dbContext = CreateDbContext();
        var (_, user, sale) =
            await SeedIssuedSaleAsync(dbContext);

        sale.Status = SaleStatus.Draft;
        sale.SaleNumber = null;
        sale.IssuedAtUtc = null;
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RegisterPaymentAsync(
                sale.Id,
                new SalePaymentRequest
                {
                    Amount = 1000m,
                    PaymentMethod = PaymentMethod.Cash,
                    PaidAtUtc = DateTime.UtcNow
                },
                user.Id,
                CancellationToken.None));
    }

    [Fact]
    public async Task VoidPaymentAsync_ExcludesPaymentFromBalanceAndPreservesHistory()
    {
        await using var dbContext = CreateDbContext();
        var (_, user, sale) =
            await SeedIssuedSaleAsync(dbContext);

        var service = CreateService(dbContext);

        var afterPayment = await service.RegisterPaymentAsync(
            sale.Id,
            new SalePaymentRequest
            {
                Amount = 10000m,
                PaymentMethod = PaymentMethod.Sinpe,
                PaidAtUtc = DateTime.UtcNow.AddDays(-1)
            },
            user.Id,
            CancellationToken.None);

        Assert.NotNull(afterPayment);
        var paymentId = Assert.Single(afterPayment.Payments).Id;

        var result = await service.VoidPaymentAsync(
            sale.Id,
            paymentId,
            new VoidSalePaymentRequest
            {
                Reason = "Monto registrado incorrectamente."
            },
            user.Id,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0m, result.PaidTotal);
        Assert.Equal(30000m, result.OutstandingAmount);
        Assert.Equal(
            SaleBalanceStatus.Pending,
            result.BalanceStatus);

        var payment = Assert.Single(result.Payments);
        Assert.Equal(SalePaymentStatus.Voided, payment.Status);
        Assert.Equal(
            "Monto registrado incorrectamente.",
            payment.VoidReason);
        Assert.NotNull(payment.VoidedAtUtc);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNoPayments_SetsDueDateFromIssueDate()
    {
        await using var dbContext = CreateDbContext();
        var (_, _, sale) =
            await SeedIssuedSaleAsync(dbContext);

        var service = CreateService(dbContext);

        var result = await service.GetByIdAsync(
            sale.Id,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(
            sale.IssuedAtUtc!.Value.AddDays(7),
            result.NextPaymentDueAtUtc);
        Assert.Equal(
            SaleBalanceStatus.Pending,
            result.BalanceStatus);
    }

    [Fact]
    public async Task GetByIdAsync_UsesLatestActivePaymentDateForNextDueDate()
    {
        await using var dbContext = CreateDbContext();
        var (_, user, sale) =
            await SeedIssuedSaleAsync(dbContext);

        var older = DateTime.UtcNow.AddDays(-4);
        var newer = DateTime.UtcNow.AddDays(-2);

        sale.Payments.Add(new SalePayment
        {
            Amount = 5000m,
            PaymentMethod = PaymentMethod.Cash,
            PaidAtUtc = newer,
            Status = SalePaymentStatus.Active,
            CreatedByUserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow
        });

        sale.Payments.Add(new SalePayment
        {
            Amount = 5000m,
            PaymentMethod = PaymentMethod.Sinpe,
            PaidAtUtc = older,
            Status = SalePaymentStatus.Active,
            CreatedByUserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.GetByIdAsync(
            sale.Id,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(
            newer.AddDays(7),
            result.NextPaymentDueAtUtc);
    }

    private static async Task<
        (Customer Customer, ApplicationUser User, Sale Sale)>
        SeedIssuedSaleAsync(
            RevestikDbContext dbContext)
    {
        var customer = new Customer
        {
            IdentificationType =
                IdentificationType.LegalEntity,
            IdentificationNumber = "3101234567",
            Name = "Payment Customer",
            Email = "payment@example.com",
            PhoneNumber = "88888888",
            ProvinceCode = "4",
            CantonCode = "01",
            DistrictCode = "01",
            OtherSigns = "Test",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "payment@test.com",
            Email = "payment@test.com",
            EmailConfirmed = true,
            DisplayName = "Payment Test User",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var issuedAt = DateTime.UtcNow.AddDays(-3);

        var sale = new Sale
        {
            SaleNumber = "VEN-900200",
            CustomerId = customer.Id,
            CreatedByUserId = user.Id,
            IssuedByUserId = user.Id,
            Currency = Currency.CRC,
            Status = SaleStatus.Issued,
            CustomerNameSnapshot = customer.Name,
            CustomerIdentificationNumberSnapshot =
                customer.IdentificationNumber,
            CustomerEmailSnapshot = customer.Email,
            CustomerPhoneNumberSnapshot =
                customer.PhoneNumber,
            IssuedAtUtc = issuedAt,
            CreatedAtUtc = issuedAt,
            Lines =
            [
                new SaleLine
                {
                    Description = "Producto",
                    Unit = "Unidad",
                    Quantity = 1m,
                    UnitPrice = 30000m,
                    TaxRate = 0m
                }
            ]
        };

        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        return (customer, user, sale);
    }

    private static SaleService CreateService(
        RevestikDbContext dbContext)
    {
        return new SaleService(
            dbContext,
            new FakeSaleNumberGenerator());
    }

    private static RevestikDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<RevestikDbContext>()
                .UseInMemoryDatabase(
                    $"SalePayment-{Guid.NewGuid()}")
                .Options;

        return new RevestikDbContext(options);
    }

    private sealed class FakeSaleNumberGenerator
        : ISaleNumberGenerator
    {
        public Task<string> GenerateAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult("VEN-999999");
        }
    }
}