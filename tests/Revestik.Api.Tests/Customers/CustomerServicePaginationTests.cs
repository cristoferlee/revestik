using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.Customers;
using Revestik.Shared.Customers;

namespace Revestik.Api.Tests.Customers;

public sealed class CustomerServicePaginationTests
{
    [Fact]
    public async Task GetPageAsync_OrdersNumericNamedLegalEntitiesFirst()
    {
        await using var dbContext = CreateDbContext();
        await SeedCustomersAsync(dbContext);

        var service = new CustomerService(dbContext);

        var result = await service.GetPageAsync(
            new CustomerListRequest
            {
                Page = 1,
                PageSize = 20
            },
            CancellationToken.None);

        Assert.Equal(
            [
                "3101000000",
                "3102000000",
                "Alpha Customer",
                "Alpha Customer",
                "Zulu Customer"
            ],
            result.Items.Select(customer => customer.Name));

        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task GetPageAsync_WithIdentificationType_FiltersResults()
    {
        await using var dbContext = CreateDbContext();
        await SeedCustomersAsync(dbContext);

        var service = new CustomerService(dbContext);

        var result = await service.GetPageAsync(
            new CustomerListRequest
            {
                IdentificationType = IdentificationType.LegalEntity,
                Page = 1,
                PageSize = 20
            },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);

        Assert.All(
            result.Items,
            customer => Assert.Equal(
                IdentificationType.LegalEntity,
                customer.IdentificationType));
    }

    [Fact]
    public async Task GetPageAsync_WithSearch_FiltersByNameOnly()
    {
        await using var dbContext = CreateDbContext();
        await SeedCustomersAsync(dbContext);

        var service = new CustomerService(dbContext);

        var result = await service.GetPageAsync(
            new CustomerListRequest
            {
                Search = "123456789",
                Page = 1,
                PageSize = 20
            },
            CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetPageAsync_WithSecondPage_ReturnsExpectedItemsAndMetadata()
    {
        await using var dbContext = CreateDbContext();
        await SeedCustomersAsync(dbContext);

        var service = new CustomerService(dbContext);

        var result = await service.GetPageAsync(
            new CustomerListRequest
            {
                Page = 2,
                PageSize = 2
            },
            CancellationToken.None);

        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.Items.Count);
        Assert.All(
            result.Items,
            customer => Assert.Equal(
                "Alpha Customer",
                customer.Name));
    }

    [Fact]
    public async Task GetPageAsync_WithPageBeyondResults_ReturnsEmptyPage()
    {
        await using var dbContext = CreateDbContext();
        await SeedCustomersAsync(dbContext);

        var service = new CustomerService(dbContext);

        var result = await service.GetPageAsync(
            new CustomerListRequest
            {
                Page = int.MaxValue,
                PageSize = 100
            },
            CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(5, result.TotalCount);
    }

    private static RevestikDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase(
                $"CustomerPaginationTests-{Guid.NewGuid()}")
            .Options;

        return new RevestikDbContext(options);
    }

    private static async Task SeedCustomersAsync(
        RevestikDbContext dbContext)
    {
        dbContext.Customers.AddRange(
            CreateCustomer(
                "Zulu Customer",
                IdentificationType.PhysicalPerson,
                "123456789"),
            CreateCustomer(
                "3102000000",
                IdentificationType.LegalEntity,
                "3102000000"),
            CreateCustomer(
                "3101000000",
                IdentificationType.LegalEntity,
                "3101000000"),
            CreateCustomer(
                "Alpha Customer",
                IdentificationType.Dimex,
                "12345678901"),
            CreateCustomer(
                "Alpha Customer",
                IdentificationType.PhysicalPerson,
                "987654321"));

        await dbContext.SaveChangesAsync();
    }

    private static Customer CreateCustomer(
        string name,
        IdentificationType identificationType,
        string identificationNumber)
    {
        return new Customer
        {
            Name = name,
            IdentificationType = identificationType,
            IdentificationNumber = identificationNumber,
            Email = "customer@example.com",
            PhoneNumber = "88888888",
            ProvinceCode = "4",
            CantonCode = "01",
            DistrictCode = "01",
            OtherSigns = "Sample address details",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}