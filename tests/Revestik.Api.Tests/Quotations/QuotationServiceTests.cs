using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
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
        var user = CreateUser();

        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var quotation = new Quotation
        {
            QuotationNumber = "COT-000001",
            CustomerId = customer.Id,
            CreatedByUserId = user.Id,
            Currency = Currency.CRC,
            Status = QuotationStatus.Draft,
            IssuedAtUtc = null,
            ValidUntilUtc = DateTime.UtcNow.AddDays(30),
            Observations = "Condiciones de prueba",
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
        Assert.Equal(QuotationStatus.Draft, result.Status);
        Assert.Null(result.IssuedAtUtc);
        Assert.Equal(
            "Condiciones de prueba",
            result.Observations);
        Assert.Equal(user.Id, result.CreatedByUserId);
        Assert.Equal(
            "Seller Test",
            result.CreatedByDisplayName);
    }

    [Fact]
    public async Task GetByIdAsync_WithLines_CalculatesLineAndQuotationTotals()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        var user = CreateUser();

        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var quotation = new Quotation
        {
            QuotationNumber = "COT-000001",
            CustomerId = customer.Id,
            CreatedByUserId = user.Id,
            Currency = Currency.CRC,
            Status = QuotationStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            Lines =
            [
                new QuotationLine
                {
                    CabysCode = "1234567890123",
                    Description = "Porcelanato 60x120",
                    Unit = "m²",
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
        var user = CreateUser();

        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var quotation = new Quotation
        {
            QuotationNumber = "COT-000001",
            CustomerId = customer.Id,
            CreatedByUserId = user.Id,
            Currency = Currency.CRC,
            Status = QuotationStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            Lines =
            [
                new QuotationLine
                {
                    CabysCode = "1234567890123",
                    Description = "Porcelanato 60x120",
                    Unit = "m²",
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
                    Amount = 25000m
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
    }

    // -------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_WhenCustomerDoesNotExist_Throws()
    {
        await using var dbContext = CreateDbContext();

        var user = CreateUser();

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request = CreateValidQuotationRequest(
            customerId: 999);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateAsync(
                    request,
                    user.Id,
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

        var user = CreateUser();

        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request =
            CreateValidQuotationRequest(customer.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(
                request,
                user.Id,
                CancellationToken.None));

        Assert.Empty(dbContext.Quotations);
    }

    [Fact]
    public async Task CreateAsync_WhenCreatorDoesNotExist_Throws()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request =
            CreateValidQuotationRequest(customer.Id);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateAsync(
                    request,
                    "missing-user-id",
                    CancellationToken.None));

        Assert.Equal(
            "The creator user does not exist or is inactive.",
            exception.Message);

        Assert.Empty(dbContext.Quotations);
    }

    [Fact]
    public async Task CreateAsync_WhenCreatorIsInactive_Throws()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        var user = CreateUser();
        user.IsActive = false;

        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request =
            CreateValidQuotationRequest(customer.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(
                request,
                user.Id,
                CancellationToken.None));

        Assert.Empty(dbContext.Quotations);
    }

    [Fact]
    public async Task CreateAsync_WithActiveCustomerAndCreator_PersistsDraft()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        var user = CreateUser();

        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = CreateService(
            dbContext,
            "COT-000123");

        var request =
            CreateValidQuotationRequest(customer.Id);

        var result = await service.CreateAsync(
            request,
            user.Id,
            CancellationToken.None);

        Assert.True(result.Id > 0);
        Assert.Equal("COT-000123", result.QuotationNumber);
        Assert.Equal(customer.Id, result.CustomerId);
        Assert.Equal(Currency.CRC, result.Currency);
        Assert.Equal(QuotationStatus.Draft, result.Status);
        Assert.Null(result.IssuedAtUtc);
        Assert.Equal(user.Id, result.CreatedByUserId);
        Assert.Equal(
            "Seller Test",
            result.CreatedByDisplayName);

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

        Assert.Equal(
            user.Id,
            persistedQuotation.CreatedByUserId);

        Assert.Equal(
            QuotationStatus.Draft,
            persistedQuotation.Status);

        Assert.Null(persistedQuotation.IssuedAtUtc);
    }

    [Fact]
    public async Task CreateAsync_PersistsLines()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        var user = CreateUser();

        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request =
            CreateValidQuotationRequest(customer.Id);

        var result = await service.CreateAsync(
            request,
            user.Id,
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

        Assert.Equal("m²", persistedLine.Unit);
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
    public async Task CreateAsync_WithEmptyCabys_PersistsNullCabys()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        var user = CreateUser();

        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request =
            CreateValidQuotationRequest(customer.Id);

        request.Lines[0].CabysCode = "   ";

        var result = await service.CreateAsync(
            request,
            user.Id,
            CancellationToken.None);

        var persistedLine =
            await dbContext.QuotationLines
                .AsNoTracking()
                .SingleAsync();

        Assert.Null(persistedLine.CabysCode);

        var responseLine = Assert.Single(result.Lines);

        Assert.Equal(
            string.Empty,
            responseLine.CabysCode);
    }

    [Fact]
    public async Task CreateAsync_PersistsCharges()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        var user = CreateUser();

        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request =
            CreateValidQuotationRequest(customer.Id);

        var result = await service.CreateAsync(
            request,
            user.Id,
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
    }

    [Fact]
    public async Task CreateAsync_TrimsText()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        var user = CreateUser();

        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request =
            CreateValidQuotationRequest(customer.Id);

        request.Observations =
            "  Condiciones especiales  ";

        request.Lines[0].CabysCode =
            "  1234567890123  ";

        request.Lines[0].Description =
            "  Porcelanato 60x120  ";

        request.Lines[0].Unit =
            "  m²  ";

        request.Charges[0].Description =
            "  Delivery to project  ";

        var result = await service.CreateAsync(
            request,
            user.Id,
            CancellationToken.None);

        var line =
            await dbContext.QuotationLines
                .AsNoTracking()
                .SingleAsync();

        var charge =
            await dbContext.QuotationCharges
                .AsNoTracking()
                .SingleAsync();

        var quotation =
            await dbContext.Quotations
                .AsNoTracking()
                .SingleAsync();

        Assert.Equal(
            "1234567890123",
            line.CabysCode);

        Assert.Equal(
            "Porcelanato 60x120",
            line.Description);

        Assert.Equal("m²", line.Unit);

        Assert.Equal(
            "Delivery to project",
            charge.Description);

        Assert.Equal(
            "Condiciones especiales",
            quotation.Observations);

        Assert.Equal(
            "Condiciones especiales",
            result.Observations);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCalculatedTotals()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        var user = CreateUser();

        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request =
            CreateValidQuotationRequest(customer.Id);

        var result = await service.CreateAsync(
            request,
            user.Id,
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
    public async Task CreateAsync_PreservesValidityDateAndObservations()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        var user = CreateUser();

        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var validUntil =
            DateTime.UtcNow.AddDays(30);

        var request =
            CreateValidQuotationRequest(customer.Id);

        request.ValidUntilUtc = validUntil;
        request.Observations = "Oferta válida según condiciones.";

        var result = await service.CreateAsync(
            request,
            user.Id,
            CancellationToken.None);

        Assert.Equal(
            validUntil,
            result.ValidUntilUtc);

        Assert.Equal(
            "Oferta válida según condiciones.",
            result.Observations);
    }


    // -------------------------------------------------------------------------
    // IssueAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task IssueAsync_WhenQuotationDoesNotExist_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var request = CreateValidQuotationRequest(customer.Id);

        var result = await service.IssueAsync(
            999,
            request,
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task IssueAsync_WhenCustomerDoesNotExist_Throws()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        var user = CreateUser();

        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, "COT-000777");
        var createRequest = CreateValidQuotationRequest(customer.Id);

        var created = await service.CreateAsync(
            createRequest,
            user.Id,
            CancellationToken.None);

        var issueRequest = CreateValidQuotationRequest(999);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.IssueAsync(
                created.Id,
                issueRequest,
                CancellationToken.None));

        Assert.Equal(
            "The customer does not exist or is inactive.",
            exception.Message);
    }

    [Fact]
    public async Task IssueAsync_WhenQuotationExists_TransitionsDraftToIssued()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        var user = CreateUser();

        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, "COT-000777");
        var request = CreateValidQuotationRequest(customer.Id);

        var created = await service.CreateAsync(
            request,
            user.Id,
            CancellationToken.None);

        var beforeIssue = DateTime.UtcNow;

        var result = await service.IssueAsync(
            created.Id,
            request,
            CancellationToken.None);

        var afterIssue = DateTime.UtcNow;

        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal("COT-000777", result.QuotationNumber);
        Assert.Equal(QuotationStatus.Issued, result.Status);
        Assert.NotNull(result.IssuedAtUtc);
        Assert.InRange(result.IssuedAtUtc.Value, beforeIssue, afterIssue);
        Assert.Equal(user.Id, result.CreatedByUserId);

        var persistedQuotation = await dbContext.Quotations
            .AsNoTracking()
            .SingleAsync(quotation => quotation.Id == created.Id);

        Assert.Equal(QuotationStatus.Issued, persistedQuotation.Status);
        Assert.NotNull(persistedQuotation.IssuedAtUtc);
        Assert.Equal("COT-000777", persistedQuotation.QuotationNumber);
        Assert.Equal(user.Id, persistedQuotation.CreatedByUserId);
    }

    [Fact]
    public async Task IssueAsync_UpdatesQuotationContentBeforeIssuing()
    {
        await using var dbContext = CreateDbContext();

        var customer = CreateCustomer();
        var user = CreateUser();

        dbContext.Customers.Add(customer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, "COT-000888");
        var createRequest = CreateValidQuotationRequest(customer.Id);

        var created = await service.CreateAsync(
            createRequest,
            user.Id,
            CancellationToken.None);

        var issueRequest = CreateValidQuotationRequest(customer.Id);
        issueRequest.Currency = Currency.USD;
        issueRequest.Observations = "  Condiciones finales de emisión  ";
        issueRequest.Lines[0].Description = "Porcelanato actualizado";
        issueRequest.Lines[0].Quantity = 3m;
        issueRequest.Charges[0].Type = QuotationChargeType.Installation;
        issueRequest.Charges[0].Description = "  Instalación proyecto  ";
        issueRequest.Charges[0].Amount = 30000m;

        var result = await service.IssueAsync(
            created.Id,
            issueRequest,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal("COT-000888", result.QuotationNumber);
        Assert.Equal(user.Id, result.CreatedByUserId);
        Assert.Equal(QuotationStatus.Issued, result.Status);
        Assert.Equal(Currency.USD, result.Currency);
        Assert.Equal("Condiciones finales de emisión", result.Observations);

        var line = Assert.Single(result.Lines);
        Assert.Equal("Porcelanato actualizado", line.Description);
        Assert.Equal(3m, line.Quantity);

        var charge = Assert.Single(result.Charges);
        Assert.Equal(QuotationChargeType.Installation, charge.Type);
        Assert.Equal("Instalación proyecto", charge.Description);
        Assert.Equal(30000m, charge.Amount);
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

    private static ApplicationUser CreateUser()
    {
        return new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "seller@test.com",
            Email = "seller@test.com",
            EmailConfirmed = true,
            DisplayName = "Seller Test",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
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
            Observations = string.Empty,

            Lines =
            [
                new QuotationLineRequest
                {
                    CabysCode = "1234567890123",
                    Description = "Porcelanato 60x120",
                    Unit = "m²",
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

                    Amount = 25000m
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