using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.Customers;
using Revestik.Shared.Customers;

namespace Revestik.Api.Tests.Customers;

public sealed class CustomerServiceLifecycleTests
{
    [Fact]
    public async Task DeactivateAsync_WithActiveCustomer_SetsIsActiveToFalse()
    {
        await using var dbContext = CreateDbContext();
        var customer = CreateCustomer(isActive: true);
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var service = new CustomerService(dbContext);

        var result = await service.DeactivateAsync(
            customer.Id,
            CancellationToken.None);

        Assert.True(result);
        Assert.False(customer.IsActive);

        var persistedCustomer = await dbContext.Customers
            .AsNoTracking()
            .SingleAsync(savedCustomer => savedCustomer.Id == customer.Id);

        Assert.False(persistedCustomer.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_WithInactiveCustomerAndActiveRequest_SetsIsActiveToTrue()
    {
        await using var dbContext = CreateDbContext();
        var customer = CreateCustomer(isActive: false);
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var service = new CustomerService(dbContext);
        var request = CreateUpdateRequest(isActive: true);

        var result = await service.UpdateAsync(
            customer.Id,
            request,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsActive);

        var persistedCustomer = await dbContext.Customers
            .AsNoTracking()
            .SingleAsync(savedCustomer => savedCustomer.Id == customer.Id);

        Assert.True(persistedCustomer.IsActive);
        Assert.Equal(request.Name, persistedCustomer.Name);
        Assert.Equal(request.IdentificationType, persistedCustomer.IdentificationType);
        Assert.Equal(request.IdentificationNumber, persistedCustomer.IdentificationNumber);
        Assert.Equal(request.Email, persistedCustomer.Email);
        Assert.Equal(request.PhoneNumber, persistedCustomer.PhoneNumber);
        Assert.Equal(request.ProvinceCode, persistedCustomer.ProvinceCode);
        Assert.Equal(request.CantonCode, persistedCustomer.CantonCode);
        Assert.Equal(request.DistrictCode, persistedCustomer.DistrictCode);
        Assert.Equal(request.OtherSigns, persistedCustomer.OtherSigns);
    }

    [Fact]
    public async Task DeactivateAsync_WithMissingCustomer_ReturnsFalse()
    {
        await using var dbContext = CreateDbContext();
        var service = new CustomerService(dbContext);

        var result = await service.DeactivateAsync(
            999,
            CancellationToken.None);

        Assert.False(result);
    }

    private static RevestikDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase(
                $"CustomerLifecycleTests-{Guid.NewGuid()}")
            .Options;

        return new RevestikDbContext(options);
    }

    private static Customer CreateCustomer(bool isActive)
    {
        return new Customer
        {
            Name = "Original Customer",
            IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = "123456789",
            Email = "original@example.com",
            PhoneNumber = "88888888",
            ProvinceCode = "4",
            CantonCode = "01",
            DistrictCode = "01",
            OtherSigns = "Original address details",
            IsActive = isActive,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static CustomerUpsertRequest CreateUpdateRequest(bool isActive)
    {
        return new CustomerUpsertRequest
        {
            Name = "Updated Customer",
            IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = "123456789",
            Email = "updated@example.com",
            PhoneNumber = "87777777",
            ProvinceCode = "1",
            CantonCode = "02",
            DistrictCode = "03",
            OtherSigns = "Updated address details",
            IsActive = isActive
        };
    }
}
