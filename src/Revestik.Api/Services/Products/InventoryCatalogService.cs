using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Products;

namespace Revestik.Api.Services.Products;

public sealed class InventoryCatalogService(
    RevestikDbContext dbContext)
    : IInventoryCatalogService
{
    public async Task<IReadOnlyList<ProductCategoryResponse>>
        GetCategoriesAsync(
            bool includeInactive,
            CancellationToken cancellationToken)
    {
        var query = dbContext.ProductCategories.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(category => category.IsActive);
        }

        return await query
            .OrderBy(category => category.Name)
            .ThenBy(category => category.Id)
            .Select(category => new ProductCategoryResponse(
                category.Id,
                category.Name,
                category.IsActive,
                category.CreatedAtUtc,
                category.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductCategoryResponse?> GetCategoryByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await dbContext.ProductCategories
            .AsNoTracking()
            .Where(category => category.Id == id)
            .Select(category => new ProductCategoryResponse(
                category.Id,
                category.Name,
                category.IsActive,
                category.CreatedAtUtc,
                category.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ProductCategoryResponse> CreateCategoryAsync(
        ProductCategoryUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var name = NormalizeRequired(request.Name);

        await EnsureCategoryNameIsAvailableAsync(
            name,
            excludedId: null,
            cancellationToken);

        var category = new ProductCategory
        {
            Name = name,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.ProductCategories.Add(category);
        await SaveChangesAsync(cancellationToken);

        return MapCategory(category);
    }

    public async Task<ProductCategoryResponse?> UpdateCategoryAsync(
        int id,
        ProductCategoryUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.ProductCategories
            .SingleOrDefaultAsync(
                category => category.Id == id,
                cancellationToken);

        if (category is null)
        {
            return null;
        }

        var name = NormalizeRequired(request.Name);

        await EnsureCategoryNameIsAvailableAsync(
            name,
            id,
            cancellationToken);

        category.Name = name;
        category.IsActive = request.IsActive;
        category.UpdatedAtUtc = DateTime.UtcNow;

        await SaveChangesAsync(cancellationToken);

        return MapCategory(category);
    }

    public async Task<bool> DeactivateCategoryAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.ProductCategories
            .SingleOrDefaultAsync(
                category => category.Id == id,
                cancellationToken);

        if (category is null)
        {
            return false;
        }

        if (category.IsActive)
        {
            category.IsActive = false;
            category.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<IReadOnlyList<UnitOfMeasureResponse>> GetUnitsAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.UnitsOfMeasure.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(unit => unit.IsActive);
        }

        return await query
            .OrderBy(unit => unit.Name)
            .ThenBy(unit => unit.Id)
            .Select(unit => new UnitOfMeasureResponse(
                unit.Id,
                unit.Name,
                unit.Symbol,
                unit.IsActive,
                unit.CreatedAtUtc,
                unit.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<UnitOfMeasureResponse?> GetUnitByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await dbContext.UnitsOfMeasure
            .AsNoTracking()
            .Where(unit => unit.Id == id)
            .Select(unit => new UnitOfMeasureResponse(
                unit.Id,
                unit.Name,
                unit.Symbol,
                unit.IsActive,
                unit.CreatedAtUtc,
                unit.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<UnitOfMeasureResponse> CreateUnitAsync(
        UnitOfMeasureUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var name = NormalizeRequired(request.Name);
        var symbol = NormalizeRequired(request.Symbol);

        await EnsureUnitValuesAreAvailableAsync(
            name,
            symbol,
            excludedId: null,
            cancellationToken);

        var unit = new UnitOfMeasure
        {
            Name = name,
            Symbol = symbol,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.UnitsOfMeasure.Add(unit);
        await SaveChangesAsync(cancellationToken);

        return MapUnit(unit);
    }

    public async Task<UnitOfMeasureResponse?> UpdateUnitAsync(
        int id,
        UnitOfMeasureUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var unit = await dbContext.UnitsOfMeasure
            .SingleOrDefaultAsync(
                unit => unit.Id == id,
                cancellationToken);

        if (unit is null)
        {
            return null;
        }

        var name = NormalizeRequired(request.Name);
        var symbol = NormalizeRequired(request.Symbol);

        await EnsureUnitValuesAreAvailableAsync(
            name,
            symbol,
            id,
            cancellationToken);

        unit.Name = name;
        unit.Symbol = symbol;
        unit.IsActive = request.IsActive;
        unit.UpdatedAtUtc = DateTime.UtcNow;

        await SaveChangesAsync(cancellationToken);

        return MapUnit(unit);
    }

    public async Task<bool> DeactivateUnitAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var unit = await dbContext.UnitsOfMeasure
            .SingleOrDefaultAsync(
                unit => unit.Id == id,
                cancellationToken);

        if (unit is null)
        {
            return false;
        }

        if (unit.IsActive)
        {
            unit.IsActive = false;
            unit.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private async Task EnsureCategoryNameIsAvailableAsync(
        string name,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.ProductCategories
            .AsNoTracking()
            .AnyAsync(
                category =>
                    category.Name == name &&
                    (!excludedId.HasValue || category.Id != excludedId),
                cancellationToken);

        if (exists)
        {
            throw new DuplicateInventoryCatalogValueException(
                "Ya existe una categoría con este nombre.");
        }
    }

    private async Task EnsureUnitValuesAreAvailableAsync(
        string name,
        string symbol,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        var duplicate = await dbContext.UnitsOfMeasure
            .AsNoTracking()
            .Where(unit =>
                !excludedId.HasValue || unit.Id != excludedId)
            .Select(unit => new
            {
                NameExists = unit.Name == name,
                SymbolExists = unit.Symbol == symbol
            })
            .FirstOrDefaultAsync(
                match => match.NameExists || match.SymbolExists,
                cancellationToken);

        if (duplicate is null)
        {
            return;
        }

        var message = duplicate.NameExists
            ? "Ya existe una unidad con este nombre."
            : "Ya existe una unidad con este símbolo.";

        throw new DuplicateInventoryCatalogValueException(message);
    }

    private async Task SaveChangesAsync(
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
            throw new DuplicateInventoryCatalogValueException(
                "El nombre o símbolo ya está registrado.",
                exception);
        }
    }

    private static ProductCategoryResponse MapCategory(
        ProductCategory category)
    {
        return new ProductCategoryResponse(
            category.Id,
            category.Name,
            category.IsActive,
            category.CreatedAtUtc,
            category.UpdatedAtUtc);
    }

    private static UnitOfMeasureResponse MapUnit(UnitOfMeasure unit)
    {
        return new UnitOfMeasureResponse(
            unit.Id,
            unit.Name,
            unit.Symbol,
            unit.IsActive,
            unit.CreatedAtUtc,
            unit.UpdatedAtUtc);
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