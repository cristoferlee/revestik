using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Customers;

namespace Revestik.Api.Services.Customers;

public sealed class CustomerService(
    RevestikDbContext dbContext)
    : ICustomerService
{
    public async Task<IReadOnlyList<CustomerResponse>> GetAllAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Customers
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();

            query = query.Where(customer =>
                customer.Name.Contains(normalizedSearch) ||
                (customer.IdentificationNumber != null &&
                 customer.IdentificationNumber.Contains(normalizedSearch)));
        }

        return await query
            .OrderBy(customer => customer.Name)
            .Select(customer => new CustomerResponse(
                customer.Id,
                customer.Name,
                customer.IdentificationType,
                customer.IdentificationNumber,
                customer.Email,
                customer.PhoneNumber,
                customer.ProvinceCode,
                customer.CantonCode,
                customer.DistrictCode,
                customer.OtherSigns,
                customer.IsActive,
                customer.CreatedAtUtc,
                customer.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.Id == id)
            .Select(customer => new CustomerResponse(
                customer.Id,
                customer.Name,
                customer.IdentificationType,
                customer.IdentificationNumber,
                customer.Email,
                customer.PhoneNumber,
                customer.ProvinceCode,
                customer.CantonCode,
                customer.DistrictCode,
                customer.OtherSigns,
                customer.IsActive,
                customer.CreatedAtUtc,
                customer.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CustomerResponse> CreateAsync(
        CustomerUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var customer = new Customer
        {
            Name = NormalizeRequired(request.Name),
            IdentificationType = request.IdentificationType,
            IdentificationNumber =
                NormalizeRequired(request.IdentificationNumber),
            Email = NormalizeRequired(request.Email),
            PhoneNumber = NormalizeRequired(request.PhoneNumber),
            ProvinceCode = NormalizeRequired(request.ProvinceCode),
            CantonCode = NormalizeRequired(request.CantonCode),
            DistrictCode = NormalizeRequired(request.DistrictCode),
            OtherSigns = NormalizeRequired(request.OtherSigns),
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Customers.Add(customer);

        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(customer);
    }

    public async Task<CustomerResponse?> UpdateAsync(
        int id,
        CustomerUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(
                customer => customer.Id == id,
                cancellationToken);

        if (customer is null)
        {
            return null;
        }

        customer.Name = NormalizeRequired(request.Name);
        customer.IdentificationType = request.IdentificationType;
        customer.IdentificationNumber =
            NormalizeRequired(request.IdentificationNumber);
        customer.Email = NormalizeRequired(request.Email);
        customer.PhoneNumber = NormalizeRequired(request.PhoneNumber);
        customer.ProvinceCode = NormalizeRequired(request.ProvinceCode);
        customer.CantonCode = NormalizeRequired(request.CantonCode);
        customer.DistrictCode = NormalizeRequired(request.DistrictCode);
        customer.OtherSigns = NormalizeRequired(request.OtherSigns);
        customer.IsActive = request.IsActive;
        customer.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(customer);
    }

    public async Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(
                customer => customer.Id == id,
                cancellationToken);

        if (customer is null)
        {
            return false;
        }

        customer.IsActive = false;
        customer.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static CustomerResponse MapToResponse(Customer customer)
    {
        return new CustomerResponse(
            customer.Id,
            customer.Name,
            customer.IdentificationType,
            customer.IdentificationNumber,
            customer.Email,
            customer.PhoneNumber,
            customer.ProvinceCode,
            customer.CantonCode,
            customer.DistrictCode,
            customer.OtherSigns,
            customer.IsActive,
            customer.CreatedAtUtc,
            customer.UpdatedAtUtc);
    }

    private static string NormalizeRequired(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "A required value cannot be empty.",
                nameof(value));
        }

        return value.Trim();
    }
}