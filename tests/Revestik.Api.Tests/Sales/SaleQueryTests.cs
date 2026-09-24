using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Sales;
using Revestik.Shared.Common;
using Revestik.Shared.Customers;
using Revestik.Shared.Sales;

namespace Revestik.Api.Tests.Sales;

public sealed class SaleQueryTests
{
    [Fact]
    public async Task GetPageAsync_SearchesBySaleNumberCustomerAndQuotation()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        dbContext.Users.Add(user);

        var customerA = CreateCustomer(
            "3101111111",
            "Constructora Alfa");
        var customerB = CreateCustomer(
            "3102222222",
            "Cliente Beta");

        dbContext.Customers.AddRange(
            customerA,
            customerB);
        await dbContext.SaveChangesAsync();

        var quotation = new Quotation
        {
            QuotationNumber = "COT-004321",
            CustomerId = customerA.Id,
            CreatedByUserId = user.Id,
            Status =
                Revestik.Shared.Quotations.QuotationStatus.Issued,
            Currency =
                Revestik.Shared.Quotations.Currency.CRC,
            IssuedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Quotations.Add(quotation);
        await dbContext.SaveChangesAsync();

        dbContext.Sales.AddRange(
            CreateSale(
                "VEN-000101",
                customerA,
                user,
                Currency.CRC,
                10000m,
                quotation.Id),
            CreateSale(
                "VEN-000102",
                customerB,
                user,
                Currency.CRC,
                20000m));

        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var byVen = await service.GetPageAsync(
            new SaleListRequest
            {
                Search = "VEN-000101"
            },
            CancellationToken.None);

        Assert.Single(byVen.Items);

        var byCustomer = await service.GetPageAsync(
            new SaleListRequest
            {
                Search = "Cliente Beta"
            },
            CancellationToken.None);

        Assert.Single(byCustomer.Items);
        Assert.Equal(
            "VEN-000102",
            byCustomer.Items[0].SaleNumber);

        var byCot = await service.GetPageAsync(
            new SaleListRequest
            {
                Search = "COT-004321"
            },
            CancellationToken.None);

        Assert.Single(byCot.Items);
        Assert.Equal(
            "VEN-000101",
            byCot.Items[0].SaleNumber);
    }

    [Fact]
    public async Task GetPageAsync_FiltersByBalanceStatus()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var customer = CreateCustomer(
            "3103333333",
            "Cliente Pagos");

        dbContext.Users.Add(user);
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var pending = CreateSale(
            "VEN-000201",
            customer,
            user,
            Currency.CRC,
            30000m);

        var partial = CreateSale(
            "VEN-000202",
            customer,
            user,
            Currency.CRC,
            30000m);

        partial.Payments.Add(new SalePayment
        {
            Amount = 10000m,
            PaymentMethod = PaymentMethod.Cash,
            PaidAtUtc = DateTime.UtcNow,
            Status = SalePaymentStatus.Active,
            CreatedByUserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow
        });

        var paid = CreateSale(
            "VEN-000203",
            customer,
            user,
            Currency.CRC,
            30000m);

        paid.Payments.Add(new SalePayment
        {
            Amount = 30000m,
            PaymentMethod = PaymentMethod.Sinpe,
            PaidAtUtc = DateTime.UtcNow,
            Status = SalePaymentStatus.Active,
            CreatedByUserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow
        });

        dbContext.Sales.AddRange(
            pending,
            partial,
            paid);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.GetPageAsync(
            new SaleListRequest
            {
                BalanceStatus =
                    SaleBalanceStatus.PartiallyPaid
            },
            CancellationToken.None);

        var item = Assert.Single(result.Items);

        Assert.Equal(
            "VEN-000202",
            item.SaleNumber);

        Assert.Equal(
            20000m,
            item.OutstandingAmount);
    }

    [Fact]
    public async Task GetSummaryAsync_ExcludesDraftAndVoidedAndSeparatesCurrencies()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var customer = CreateCustomer(
            "3104444444",
            "Cliente Summary");

        dbContext.Users.Add(user);
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var issuedCrc = CreateSale(
            "VEN-000301",
            customer,
            user,
            Currency.CRC,
            100000m);

        issuedCrc.Payments.Add(new SalePayment
        {
            Amount = 40000m,
            PaymentMethod = PaymentMethod.Cash,
            PaidAtUtc = DateTime.UtcNow,
            Status = SalePaymentStatus.Active,
            CreatedByUserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow
        });

        var issuedUsd = CreateSale(
            "VEN-000302",
            customer,
            user,
            Currency.USD,
            500m);

        var draft = CreateSale(
            null,
            customer,
            user,
            Currency.CRC,
            999999m);
        draft.Status = SaleStatus.Draft;
        draft.IssuedAtUtc = null;

        var voided = CreateSale(
            "VEN-000303",
            customer,
            user,
            Currency.CRC,
            70000m);
        voided.Status = SaleStatus.Voided;
        voided.VoidedAtUtc = DateTime.UtcNow;
        voided.VoidReason = "Test";

        dbContext.Sales.AddRange(
            issuedCrc,
            issuedUsd,
            draft,
            voided);

        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var summary = await service.GetSummaryAsync(
            CancellationToken.None);

        Assert.Equal(2, summary.Currencies.Count);

        var crc = summary.Currencies.Single(
            item => item.Currency == Currency.CRC);

        Assert.Equal(100000m, crc.SoldTotal);
        Assert.Equal(40000m, crc.CollectedTotal);
        Assert.Equal(60000m, crc.OutstandingTotal);
        Assert.Equal(1, crc.SaleCount);

        var usd = summary.Currencies.Single(
            item => item.Currency == Currency.USD);

        Assert.Equal(500m, usd.SoldTotal);
        Assert.Equal(0m, usd.CollectedTotal);
        Assert.Equal(500m, usd.OutstandingTotal);
        Assert.Equal(1, usd.SaleCount);
    }

    [Fact]
    public async Task GetPageAsync_PaginatesDeterministically()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var customer = CreateCustomer(
            "3105555555",
            "Cliente Paginación");

        dbContext.Users.Add(user);
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        for (var index = 1; index <= 3; index++)
        {
            var sale = CreateSale(
                $"VEN-00040{index}",
                customer,
                user,
                Currency.CRC,
                index * 1000m);

            sale.IssuedAtUtc =
                DateTime.UtcNow.AddMinutes(-index);

            dbContext.Sales.Add(sale);
        }

        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        PaginatedResponse<SaleListItemResponse> result =
            await service.GetPageAsync(
                new SaleListRequest
                {
                    Page = 2,
                    PageSize = 2
                },
                CancellationToken.None);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Single(result.Items);
    }

    private static Sale CreateSale(
        string? number,
        Customer customer,
        ApplicationUser user,
        Currency currency,
        decimal total,
        int? sourceQuotationId = null)
    {
        return new Sale
        {
            SaleNumber = number,
            SourceQuotationId = sourceQuotationId,
            CustomerId = customer.Id,
            CreatedByUserId = user.Id,
            IssuedByUserId =
                number is null ? null : user.Id,
            CustomerNameSnapshot =
                number is null ? string.Empty : customer.Name,
            CustomerIdentificationNumberSnapshot =
                number is null
                    ? string.Empty
                    : customer.IdentificationNumber,
            CustomerEmailSnapshot =
                number is null ? string.Empty : customer.Email,
            CustomerPhoneNumberSnapshot =
                number is null
                    ? string.Empty
                    : customer.PhoneNumber,
            Currency = currency,
            Status =
                number is null
                    ? SaleStatus.Draft
                    : SaleStatus.Issued,
            IssuedAtUtc =
                number is null
                    ? null
                    : DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            Lines =
            [
                new SaleLine
                {
                    Description = "Producto",
                    Unit = "Unidad",
                    Quantity = 1m,
                    UnitPrice = total,
                    TaxRate = 0m
                }
            ]
        };
    }

    private static Customer CreateCustomer(
        string identification,
        string name)
    {
        return new Customer
        {
            IdentificationType =
                IdentificationType.LegalEntity,
            IdentificationNumber = identification,
            Name = name,
            Email = $"{identification}@example.com",
            PhoneNumber = "88888888",
            ProvinceCode = "4",
            CantonCode = "01",
            DistrictCode = "01",
            OtherSigns = "Test",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static ApplicationUser CreateUser()
    {
        return new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "query@test.com",
            Email = "query@test.com",
            EmailConfirmed = true,
            DisplayName = "Query Test User",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
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
                    $"SaleQuery-{Guid.NewGuid()}")
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