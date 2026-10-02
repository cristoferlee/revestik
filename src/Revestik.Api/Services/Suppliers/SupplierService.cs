using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Common;
using Revestik.Shared.Suppliers;

namespace Revestik.Api.Services.Suppliers;

public sealed class SupplierService(RevestikDbContext dbContext)
    : ISupplierService
{
    public async Task<PaginatedResponse<SupplierListItemResponse>> GetPageAsync(
        SupplierListRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = dbContext.Suppliers.AsNoTracking();

        if (request.IncludeInactive != true)
        {
            query = query.Where(supplier => supplier.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(supplier => supplier.Name.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var skip = ((long)request.Page - 1) * request.PageSize;

        IReadOnlyList<SupplierListItemResponse> items;

        if (skip >= totalCount)
        {
            items = [];
        }
        else
        {
            items = await query
                .OrderBy(supplier => supplier.Name)
                .ThenBy(supplier => supplier.Id)
                .Skip((int)skip)
                .Take(request.PageSize)
                .Select(supplier => new SupplierListItemResponse(
                    supplier.Id,
                    supplier.Name,
                    supplier.ContactName,
                    supplier.PhoneNumber,
                    supplier.Email,
                    supplier.IsActive))
                .ToListAsync(cancellationToken);
        }

        return new PaginatedResponse<SupplierListItemResponse>(
            items,
            request.Page,
            request.PageSize,
            totalCount);
    }

    public async Task<SupplierResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await dbContext.Suppliers
            .AsNoTracking()
            .Where(supplier => supplier.Id == id)
            .Select(supplier => new SupplierResponse(
                supplier.Id,
                supplier.Name,
                supplier.ContactName,
                supplier.PhoneNumber,
                supplier.Email,
                supplier.IsActive,
                supplier.CreatedAtUtc,
                supplier.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SupplierResponse> CreateAsync(
        SupplierUpsertRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var supplier = new Supplier
        {
            Name = NormalizeRequired(request.Name),
            ContactName = NormalizeRequired(request.ContactName),
            PhoneNumber = NormalizeRequired(request.PhoneNumber),
            Email = NormalizeRequired(request.Email),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Suppliers.Add(supplier);
        await SaveSupplierChangesAsync(cancellationToken);

        return MapToResponse(supplier);
    }

    public async Task<SupplierResponse?> UpdateAsync(
        int id,
        SupplierUpsertRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var supplier = await dbContext.Suppliers
            .FirstOrDefaultAsync(
                supplier => supplier.Id == id,
                cancellationToken);

        if (supplier is null)
        {
            return null;
        }

        supplier.Name = NormalizeRequired(request.Name);
        supplier.ContactName = NormalizeRequired(request.ContactName);
        supplier.PhoneNumber = NormalizeRequired(request.PhoneNumber);
        supplier.Email = NormalizeRequired(request.Email);
        supplier.UpdatedAtUtc = DateTime.UtcNow;

        await SaveSupplierChangesAsync(cancellationToken);

        return MapToResponse(supplier);
    }

    public async Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers
            .FirstOrDefaultAsync(
                supplier => supplier.Id == id,
                cancellationToken);

        if (supplier is null)
        {
            return false;
        }

        supplier.IsActive = false;
        supplier.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> ReactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers
            .FirstOrDefaultAsync(
                supplier => supplier.Id == id,
                cancellationToken);

        if (supplier is null)
        {
            return false;
        }

        supplier.IsActive = true;
        supplier.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task SaveSupplierChangesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException
            {
                Number: 2601 or 2627
            })
        {
            throw new DuplicateSupplierNameException(exception);
        }
    }

    private static SupplierResponse MapToResponse(Supplier supplier) =>
        new(
            supplier.Id,
            supplier.Name,
            supplier.ContactName,
            supplier.PhoneNumber,
            supplier.Email,
            supplier.IsActive,
            supplier.CreatedAtUtc,
            supplier.UpdatedAtUtc);

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