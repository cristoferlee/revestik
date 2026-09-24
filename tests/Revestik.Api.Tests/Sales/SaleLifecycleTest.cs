using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Sales;
using Revestik.Shared.Customers;
using Revestik.Shared.Sales;
using QuotationCurrency = Revestik.Shared.Quotations.Currency;
using QuotationStatus = Revestik.Shared.Quotations.QuotationStatus;
using QuotationChargeType = Revestik.Shared.Quotations.QuotationChargeType;
using QuotationDiscountType = Revestik.Shared.Quotations.DiscountType;

namespace Revestik.Api.Tests.Sales;

public sealed class SaleLifecycleTests
{
    [Fact]
    public async Task CreateFromQuotationAsync_CopiesIssuedQuotationIntoDraftSale()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        var user = CreateUser();
        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var quotation = CreateIssuedQuotation(
            customer.Id,
            user.Id);

        dbContext.Quotations.Add(quotation);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result =
            await service.CreateFromQuotationAsync(
                quotation.Id,
                user.Id,
                CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(SaleStatus.Draft, result.Status);
        Assert.Equal(string.Empty, result.SaleNumber);
        Assert.Equal(quotation.Id, result.SourceQuotationId);
        Assert.Equal(
            quotation.QuotationNumber,
            result.SourceQuotationNumber);
        Assert.Equal(customer.Id, result.CustomerId);
        Assert.Equal(Currency.USD, result.Currency);

        var line = Assert.Single(result.Lines);
        Assert.Equal("Porcelanato cotizado", line.Description);
        Assert.Equal(2m, line.Quantity);
        Assert.Equal(15000m, line.UnitPrice);
        Assert.Equal(
            DiscountType.Percentage,
            line.DiscountType);

        var charge = Assert.Single(result.Charges);
        Assert.Equal(
            SaleChargeType.Transport,
            charge.Type);
        Assert.Equal(25000m, charge.Amount);
    }

    [Fact]
    public async Task CreateFromQuotationAsync_WhenQuotationIsDraft_Throws()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        var user = CreateUser();
        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var quotation = CreateIssuedQuotation(
            customer.Id,
            user.Id);

        quotation.Status = QuotationStatus.Draft;
        quotation.IssuedAtUtc = null;

        dbContext.Quotations.Add(quotation);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateFromQuotationAsync(
                quotation.Id,
                user.Id,
                CancellationToken.None));
    }

    [Fact]
    public async Task CreateFromQuotationAsync_WhenAlreadyConverted_Throws()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        var user = CreateUser();
        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var quotation = CreateIssuedQuotation(
            customer.Id,
            user.Id);

        dbContext.Quotations.Add(quotation);
        await dbContext.SaveChangesAsync();

        dbContext.Sales.Add(new Sale
        {
            SourceQuotationId = quotation.Id,
            CustomerId = customer.Id,
            CreatedByUserId = user.Id,
            Status = SaleStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateFromQuotationAsync(
                quotation.Id,
                user.Id,
                CancellationToken.None));
    }

    [Fact]
    public async Task VoidAsync_WhenIssuedAndUnpaid_VoidsSale()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        var user = CreateUser();
        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var sale = CreateIssuedSale(
            customer.Id,
            user.Id);

        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var before = DateTime.UtcNow;

        var result = await service.VoidAsync(
            sale.Id,
            new VoidSaleRequest
            {
                Reason = "Cliente desistió de la compra."
            },
            user.Id,
            CancellationToken.None);

        var after = DateTime.UtcNow;

        Assert.NotNull(result);
        Assert.Equal(SaleStatus.Voided, result.Status);
        Assert.Equal(
            "Cliente desistió de la compra.",
            result.VoidReason);
        Assert.NotNull(result.VoidedAtUtc);
        Assert.InRange(
            result.VoidedAtUtc.Value,
            before,
            after);
    }

    [Fact]
    public async Task VoidAsync_WhenSaleHasActivePayment_Throws()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        var user = CreateUser();
        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var sale = CreateIssuedSale(
            customer.Id,
            user.Id);

        sale.Payments.Add(new SalePayment
        {
            Amount = 1000m,
            PaymentMethod = PaymentMethod.Cash,
            PaidAtUtc = DateTime.UtcNow,
            Status = SalePaymentStatus.Active,
            CreatedByUserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow
        });

        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.VoidAsync(
                sale.Id,
                new VoidSaleRequest
                {
                    Reason = "Corrección requerida."
                },
                user.Id,
                CancellationToken.None));
    }

    [Fact]
    public async Task CreateReplacementAsync_VoidsOriginalAndCreatesLinkedDraft()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        var user = CreateUser();
        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var original = CreateIssuedSale(
            customer.Id,
            user.Id);

        dbContext.Sales.Add(original);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var replacement =
            await service.CreateReplacementAsync(
                original.Id,
                new VoidSaleRequest
                {
                    Reason = "Precio incorrecto."
                },
                user.Id,
                CancellationToken.None);

        Assert.NotNull(replacement);
        Assert.Equal(
            SaleStatus.Draft,
            replacement.Status);
        Assert.Equal(
            string.Empty,
            replacement.SaleNumber);
        Assert.Equal(
            original.Id,
            replacement.ReplacesSaleId);

        var persistedOriginal =
            await dbContext.Sales
                .AsNoTracking()
                .SingleAsync(
                    sale => sale.Id == original.Id);

        Assert.Equal(
            SaleStatus.Voided,
            persistedOriginal.Status);

        Assert.Equal(
            "Precio incorrecto.",
            persistedOriginal.VoidReason);

        var copiedLine =
            Assert.Single(replacement.Lines);

        Assert.Equal(
            original.Lines.Single().Description,
            copiedLine.Description);

        Assert.Equal(
            original.Lines.Single().UnitPrice,
            copiedLine.UnitPrice);
    }

    [Fact]
    public async Task CreateReplacementAsync_WhenReplacementAlreadyExists_Throws()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        var user = CreateUser();
        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var original = CreateIssuedSale(
            customer.Id,
            user.Id);

        dbContext.Sales.Add(original);
        await dbContext.SaveChangesAsync();

        var existingReplacement = new Sale
        {
            CustomerId = customer.Id,
            CreatedByUserId = user.Id,
            ReplacesSaleId = original.Id,
            Status = SaleStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Sales.Add(existingReplacement);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateReplacementAsync(
                original.Id,
                new VoidSaleRequest
                {
                    Reason = "Otra corrección."
                },
                user.Id,
                CancellationToken.None));
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
                    $"SaleLifecycle-{Guid.NewGuid()}")
                .Options;

        return new RevestikDbContext(options);
    }

    private static Customer CreateCustomer()
    {
        return new Customer
        {
            IdentificationType =
                IdentificationType.LegalEntity,
            IdentificationNumber = "3101234567",
            Name = "Cliente Sales Test",
            Email = "sales@example.com",
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
            UserName = "sales@test.com",
            Email = "sales@test.com",
            EmailConfirmed = true,
            DisplayName = "Sales Test User",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static Quotation CreateIssuedQuotation(
        int customerId,
        string userId)
    {
        return new Quotation
        {
            QuotationNumber = "COT-900100",
            CustomerId = customerId,
            CreatedByUserId = userId,
            Currency = QuotationCurrency.USD,
            Status = QuotationStatus.Issued,
            IssuedAtUtc = DateTime.UtcNow,
            ValidUntilUtc = DateTime.UtcNow.AddDays(30),
            Observations = "Desde cotización",
            CreatedAtUtc = DateTime.UtcNow,
            Lines =
            [
                new QuotationLine
                {
                    CabysCode = "1234567890123",
                    Description = "Porcelanato cotizado",
                    Unit = "m²",
                    Quantity = 2m,
                    UnitPrice = 15000m,
                    DiscountType =
                        QuotationDiscountType.Percentage,
                    DiscountValue = 10m,
                    TaxRate = 13m
                }
            ],
            Charges =
            [
                new QuotationCharge
                {
                    Type =
                        QuotationChargeType.Transport,
                    Description = "Transporte",
                    Amount = 25000m
                }
            ]
        };
    }

    private static Sale CreateIssuedSale(
        int customerId,
        string userId)
    {
        var sale = new Sale
        {
            SaleNumber = "VEN-900001",
            CustomerId = customerId,
            CreatedByUserId = userId,
            IssuedByUserId = userId,
            Currency = Currency.CRC,
            Status = SaleStatus.Issued,
            CustomerNameSnapshot = "Cliente Sales Test",
            CustomerIdentificationNumberSnapshot =
                "3101234567",
            CustomerEmailSnapshot =
                "sales@example.com",
            CustomerPhoneNumberSnapshot =
                "88888888",
            IssuedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            Lines =
            [
                new SaleLine
                {
                    Description = "Porcelanato venta",
                    Unit = "m²",
                    Quantity = 2m,
                    UnitPrice = 15000m,
                    TaxRate = 13m
                }
            ]
        };

        return sale;
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