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
                customer.IdentificationNumber,
                customer.Email,
                customer.PhoneNumber,
                customer.Address,
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
                customer.IdentificationNumber,
                customer.Email,
                customer.PhoneNumber,
                customer.Address,
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
            IdentificationNumber =
                NormalizeOptional(request.IdentificationNumber),
            Email = NormalizeOptional(request.Email),
            PhoneNumber = NormalizeOptional(request.PhoneNumber),
            Address = NormalizeOptional(request.Address),
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
        customer.IdentificationNumber =
            NormalizeOptional(request.IdentificationNumber);
        customer.Email = NormalizeOptional(request.Email);
        customer.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        customer.Address = NormalizeOptional(request.Address);
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
            customer.IdentificationNumber,
            customer.Email,
            customer.PhoneNumber,
            customer.Address,
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

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}