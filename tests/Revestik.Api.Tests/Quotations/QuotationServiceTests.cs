using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.Quotations;
using Revestik.Shared.Quotations;

namespace Revestik.Api.Tests.Quotations;

public sealed class QuotationServiceTests
{
    // -------------------------------------------------------------------------
    // GetByIdAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_WhenQuotationDoesNotExist_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.GetByIdAsync(
            999,
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_WhenQuotationExists_MapsQuotation()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var quotation = new Quotation
        {
            QuotationNumber = "COT-000001",
            CustomerId = customer.Id,
            Currency = Currency.CRC,
            IssuedAtUtc = DateTime.UtcNow,
            ValidUntilUtc = DateTime.UtcNow.AddDays(30),
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Quotations.Add(quotation);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.GetByIdAsync(
            quotation.Id,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(quotation.Id, result.Id);
        Assert.Equal("COT-000001", result.QuotationNumber);
        Assert.Equal(customer.Id, result.CustomerId);
        Assert.Equal(Currency.CRC, result.Currency);
    }

    [Fact]
    public async Task GetByIdAsync_WithLines_CalculatesLineAndQuotationTotals()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var quotation = new Quotation
        {
            QuotationNumber = "COT-000001",
            CustomerId = customer.Id,
            Currency = Currency.CRC,
            IssuedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            Lines =
            [
                new QuotationLine
                {
                    CabysCode = "1234567890123",
                    Description = "Porcelanato 60x120",
                    Quantity = 1m,
                    UnitPrice = 15000m,
                    DiscountValue = 0m,
                    TaxRate = 13m
                }
            ]
        };

        dbContext.Quotations.Add(quotation);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.GetByIdAsync(
            quotation.Id,
            CancellationToken.None);

        Assert.NotNull(result);

        var line = Assert.Single(result.Lines);

        Assert.Equal(13274.34m, line.BaseAmount);
        Assert.Equal(0m, line.DiscountAmount);
        Assert.Equal(1725.66m, line.TaxAmount);
        Assert.Equal(15000m, line.TotalAmount);

        Assert.Equal(13274.34m, result.Subtotal);
        Assert.Equal(0m, result.DiscountTotal);
        Assert.Equal(1725.66m, result.TaxTotal);
        Assert.Equal(0m, result.ChargeTotal);
        Assert.Equal(15000m, result.Total);
    }

    [Fact]
    public async Task GetByIdAsync_WithDiscountAndCharge_CalculatesTotals()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var quotation = new Quotation
        {
            QuotationNumber = "COT-000001",
            CustomerId = customer.Id,
            Currency = Currency.CRC,
            IssuedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            Lines =
            [
                new QuotationLine
                {
                    CabysCode = "1234567890123",
                    Description = "Porcelanato 60x120",
                    Quantity = 1m,
                    UnitPrice = 15000m,
                    DiscountType = DiscountType.Percentage,
                    DiscountValue = 10m,
                    TaxRate = 13m
                }
            ],
            Charges =
            [
                new QuotationCharge
                {
                    Type = QuotationChargeType.Transport,
                    Description = "Delivery to project",
                    Amount = 25000m,
                    TaxRate = 13m
                }
            ]
        };

        dbContext.Quotations.Add(quotation);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.GetByIdAsync(
            quotation.Id,
            CancellationToken.None);

        Assert.NotNull(result);

        Assert.Equal(11946.90m, result.Subtotal);
        Assert.Equal(1500m, result.DiscountTotal);
        Assert.Equal(1553.10m, result.TaxTotal);
        Assert.Equal(25000m, result.ChargeTotal);
        Assert.Equal(38500m, result.Total);

        var charge = Assert.Single(result.Charges);

        Assert.Equal(
            QuotationChargeType.Transport,
            charge.Type);

        Assert.Equal(
            "Delivery to project",
            charge.Description);

        Assert.Equal(25000m, charge.Amount);
        Assert.Equal(13m, charge.TaxRate);
    }

    // -------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_WhenCustomerDoesNotExist_Throws()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var request = CreateValidQuotationRequest(
            customerId: 999);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateAsync(
                    request,
                    CancellationToken.None));

        Assert.Equal(
            "The customer does not exist or is inactive.",
            exception.Message);

        Assert.Empty(dbContext.Quotations);
    }

    [Fact]
    public async Task CreateAsync_WhenCustomerIsInactive_Throws()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        customer.IsActive = false;

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request =
            CreateValidQuotationRequest(customer.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(
                request,
                CancellationToken.None));

        Assert.Empty(dbContext.Quotations);
    }

    [Fact]
    public async Task CreateAsync_WithActiveCustomer_PersistsQuotation()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var service = CreateService(
            dbContext,
            "COT-000123");

        var request =
            CreateValidQuotationRequest(customer.Id);

        var result = await service.CreateAsync(
            request,
            CancellationToken.None);

        Assert.True(result.Id > 0);
        Assert.Equal("COT-000123", result.QuotationNumber);
        Assert.Equal(customer.Id, result.CustomerId);
        Assert.Equal(Currency.CRC, result.Currency);

        var persistedQuotation =
            await dbContext.Quotations
                .AsNoTracking()
                .SingleAsync();

        Assert.Equal(
            result.Id,
            persistedQuotation.Id);

        Assert.Equal(
            "COT-000123",
            persistedQuotation.QuotationNumber);

        Assert.Equal(
            customer.Id,
            persistedQuotation.CustomerId);
    }

    [Fact]
    public async Task CreateAsync_PersistsLines()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request =
            CreateValidQuotationRequest(customer.Id);

        var result = await service.CreateAsync(
            request,
            CancellationToken.None);

        var persistedLine =
            await dbContext.QuotationLines
                .AsNoTracking()
                .SingleAsync();

        Assert.Equal(
            result.Id,
            persistedLine.QuotationId);

        Assert.Equal(
            "1234567890123",
            persistedLine.CabysCode);

        Assert.Equal(
            "Porcelanato 60x120",
            persistedLine.Description);

        Assert.Equal(2m, persistedLine.Quantity);
        Assert.Equal(15000m, persistedLine.UnitPrice);

        Assert.Equal(
            DiscountType.Percentage,
            persistedLine.DiscountType);

        Assert.Equal(
            10m,
            persistedLine.DiscountValue);

        Assert.Equal(
            13m,
            persistedLine.TaxRate);
    }

    [Fact]
    public async Task CreateAsync_PersistsCharges()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request =
            CreateValidQuotationRequest(customer.Id);

        var result = await service.CreateAsync(
            request,
            CancellationToken.None);

        var persistedCharge =
            await dbContext.QuotationCharges
                .AsNoTracking()
                .SingleAsync();

        Assert.Equal(
            result.Id,
            persistedCharge.QuotationId);

        Assert.Equal(
            QuotationChargeType.Transport,
            persistedCharge.Type);

        Assert.Equal(
            "Delivery to project",
            persistedCharge.Description);

        Assert.Equal(
            25000m,
            persistedCharge.Amount);

        Assert.Equal(
            13m,
            persistedCharge.TaxRate);
    }

    [Fact]
    public async Task CreateAsync_TrimsLineAndChargeText()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request =
            CreateValidQuotationRequest(customer.Id);

        request.Lines[0].CabysCode =
            "  1234567890123  ";

        request.Lines[0].Description =
            "  Porcelanato 60x120  ";

        request.Charges[0].Description =
            "  Delivery to project  ";

        await service.CreateAsync(
            request,
            CancellationToken.None);

        var line =
            await dbContext.QuotationLines
                .AsNoTracking()
                .SingleAsync();

        var charge =
            await dbContext.QuotationCharges
                .AsNoTracking()
                .SingleAsync();

        Assert.Equal(
            "1234567890123",
            line.CabysCode);

        Assert.Equal(
            "Porcelanato 60x120",
            line.Description);

        Assert.Equal(
            "Delivery to project",
            charge.Description);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCalculatedTotals()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request =
            CreateValidQuotationRequest(customer.Id);

        var result = await service.CreateAsync(
            request,
            CancellationToken.None);

        var line = Assert.Single(result.Lines);

        Assert.Equal(
            23893.81m,
            line.BaseAmount);

        Assert.Equal(
            3000m,
            line.DiscountAmount);

        Assert.Equal(
            3106.19m,
            line.TaxAmount);

        Assert.Equal(
            27000m,
            line.TotalAmount);

        Assert.Equal(
            23893.81m,
            result.Subtotal);

        Assert.Equal(
            3000m,
            result.DiscountTotal);

        Assert.Equal(
            3106.19m,
            result.TaxTotal);

        Assert.Equal(
            25000m,
            result.ChargeTotal);

        Assert.Equal(
            52000m,
            result.Total);
    }

    [Fact]
    public async Task CreateAsync_PreservesValidityDate()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var validUntil =
            DateTime.UtcNow.AddDays(30);

        var request =
            CreateValidQuotationRequest(customer.Id);

        request.ValidUntilUtc = validUntil;

        var result = await service.CreateAsync(
            request,
            CancellationToken.None);

        Assert.Equal(
            validUntil,
            result.ValidUntilUtc);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static QuotationService CreateService(
        RevestikDbContext dbContext,
        string quotationNumber = "COT-000001")
    {
        return new QuotationService(
            dbContext,
            new FakeQuotationNumberGenerator(
                quotationNumber));
    }

    private static RevestikDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<RevestikDbContext>()
                .UseInMemoryDatabase(
                    $"QuotationTests-{Guid.NewGuid()}")
                .Options;

        return new RevestikDbContext(options);
    }

    private static Customer CreateCustomer()
    {
        return new Customer
        {
            IdentificationType =
                Revestik.Shared.Customers
                    .IdentificationType.LegalEntity,

            IdentificationNumber = "3101234567",
            Name = "Constructora Test S.A.",
            Email = "test@example.com",
            PhoneNumber = "22222222",
            ProvinceCode = "4",
            CantonCode = "01",
            DistrictCode = "01",
            OtherSigns = "Test address",
            IsActive = true
        };
    }

    private static QuotationUpsertRequest
        CreateValidQuotationRequest(int customerId)
    {
        return new QuotationUpsertRequest
        {
            CustomerId = customerId,
            Currency = Currency.CRC,
            ValidUntilUtc =
                DateTime.UtcNow.AddDays(30),

            Lines =
            [
                new QuotationLineRequest
                {
                    CabysCode = "1234567890123",
                    Description = "Porcelanato 60x120",
                    Quantity = 2m,
                    UnitPrice = 15000m,
                    DiscountType =
                        DiscountType.Percentage,
                    DiscountValue = 10m,
                    TaxRate = 13m
                }
            ],

            Charges =
            [
                new QuotationChargeRequest
                {
                    Type =
                        QuotationChargeType.Transport,

                    Description =
                        "Delivery to project",

                    Amount = 25000m,
                    TaxRate = 13m
                }
            ]
        };
    }

    private sealed class FakeQuotationNumberGenerator(
        string quotationNumber)
        : IQuotationNumberGenerator
    {
        public Task<string> GenerateAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                quotationNumber);
        }
    }
}