using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Common;
using Revestik.Shared.Customers;

namespace Revestik.Api.Services.Customers;

public sealed class CustomerService(
    RevestikDbContext dbContext)
    : ICustomerService
{
    public async Task<PaginatedResponse<CustomerListItemResponse>> GetPageAsync(
        CustomerListRequest request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Customers
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var normalizedSearch = request.Search.Trim();

            query = query.Where(customer =>
                customer.Name.Contains(normalizedSearch));
        }

        if (request.IdentificationType.HasValue)
        {
            query = query.Where(customer =>
                customer.IdentificationType ==
                request.IdentificationType.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var skip = ((long)request.Page - 1) * request.PageSize;

        IReadOnlyList<CustomerListItemResponse> items;

        if (skip >= totalCount)
        {
            items = [];
        }
        else
        {
            items = await query
                .OrderBy(customer =>
                    customer.IdentificationType ==
                        IdentificationType.LegalEntity &&
                    customer.Name == customer.IdentificationNumber
                        ? 0
                        : 1)
                .ThenBy(customer =>
                    customer.IdentificationType ==
                        IdentificationType.LegalEntity &&
                    customer.Name == customer.IdentificationNumber
                        ? customer.IdentificationNumber
                        : null)
                .ThenBy(customer => customer.Name)
                .ThenBy(customer => customer.Id)
                .Skip((int)skip)
                .Take(request.PageSize)
                .Select(customer => new CustomerListItemResponse(
                    customer.Id,
                    customer.IdentificationNumber,
                    customer.Name,
                    customer.Email,
                    customer.PhoneNumber,
                    customer.IdentificationType,
                    customer.IsActive))
                .ToListAsync(cancellationToken);
        }

        return new PaginatedResponse<CustomerListItemResponse>(
            items,
            request.Page,
            request.PageSize,
            totalCount);
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
            IdentificationType = GetRequiredIdentificationType(
                request.IdentificationType),
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

        await SaveCustomerChangesAsync(cancellationToken);

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
        customer.IdentificationType = GetRequiredIdentificationType(
            request.IdentificationType);
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

        await SaveCustomerChangesAsync(cancellationToken);

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

    private async Task SaveCustomerChangesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            throw new DuplicateCustomerIdentificationException(exception);
        }
    }

    private static bool IsUniqueConstraintViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is SqlException
        {
            Number: 2601 or 2627
        };
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

    private static IdentificationType GetRequiredIdentificationType(
        IdentificationType? identificationType)
    {
        return identificationType
            ?? throw new ArgumentException(
                "Identification type is required.",
                nameof(identificationType));
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