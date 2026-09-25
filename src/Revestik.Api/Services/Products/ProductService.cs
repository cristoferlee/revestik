using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Common;
using Revestik.Shared.Products;

namespace Revestik.Api.Services.Products;

public sealed class ProductService(RevestikDbContext dbContext)
    : IProductService
{
    public async Task<PaginatedResponse<ProductListItemResponse>> GetPageAsync(
        ProductListRequest request,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilters(
            dbContext.Products.AsNoTracking(),
            request);

        var totalCount = await query.CountAsync(cancellationToken);
        var skip = ((long)request.Page - 1) * request.PageSize;

        IReadOnlyList<ProductListItemResponse> items;

        if (skip >= totalCount)
        {
            items = [];
        }
        else
        {
            items = await query
                .OrderBy(product => product.Name)
                .ThenBy(product => product.Id)
                .Skip((int)skip)
                .Take(request.PageSize)
                .Select(product => new ProductListItemResponse(
                    product.Id,
                    product.CategoryId,
                    product.Category.Name,
                    product.Name,
                    product.Description,
                    product.CabysCode,
                    product.InventoryUnit.Symbol,
                    product.CommercialUnit.Symbol,
                    product.CommercialUnitsPerInventoryUnit,
                    product.RequiresWholeInventoryUnits,
                    product.SalePrice,
                    product.CurrentCost,
                    product.TaxRate,
                    product.StockQuantity,
                    product.MinimumStock,
                    product.IsDeleted))
                .ToListAsync(cancellationToken);
        }

        return new PaginatedResponse<ProductListItemResponse>(
            items,
            request.Page,
            request.PageSize,
            totalCount);
    }

    public async Task<ProductResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await dbContext.Products
            .AsNoTracking()
            .Where(product => product.Id == id)
            .Select(product => new ProductResponse(
                product.Id,
                product.CategoryId,
                product.Category.Name,
                product.Name,
                product.Description,
                product.CabysCode,
                product.InventoryUnitId,
                product.InventoryUnit.Name,
                product.InventoryUnit.Symbol,
                product.CommercialUnitId,
                product.CommercialUnit.Name,
                product.CommercialUnit.Symbol,
                product.CommercialUnitsPerInventoryUnit,
                product.RequiresWholeInventoryUnits,
                product.SalePrice,
                product.CurrentCost,
                product.TaxRate,
                product.StockQuantity,
                product.MinimumStock,
                product.IsDeleted,
                product.CreatedAtUtc,
                product.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ProductResponse> CreateAsync(
        ProductUpsertRequest request,
        CancellationToken cancellationToken)
    {
        await ValidateCatalogReferencesAsync(
            request,
            currentProduct: null,
            cancellationToken);

        var product = new Product
        {
            StockQuantity = 0m,
            IsDeleted = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        ApplyRequest(product, request);

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(product.Id, cancellationToken)
            ?? throw new InvalidOperationException(
                "The created product could not be loaded.");
    }

    public async Task<ProductResponse?> UpdateAsync(
        int id,
        ProductUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .SingleOrDefaultAsync(
                product => product.Id == id && !product.IsDeleted,
                cancellationToken);

        if (product is null)
        {
            return null;
        }

        await ValidateCatalogReferencesAsync(
            request,
            product,
            cancellationToken);

        ApplyRequest(product, request);
        product.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(product.Id, cancellationToken)
            ?? throw new InvalidOperationException(
                "The updated product could not be loaded.");
    }

    public async Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .SingleOrDefaultAsync(
                product => product.Id == id,
                cancellationToken);

        if (product is null)
        {
            return false;
        }

        if (!product.IsDeleted)
        {
            product.IsDeleted = true;
            product.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<bool> ReactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .SingleOrDefaultAsync(
                product => product.Id == id,
                cancellationToken);

        if (product is null)
        {
            return false;
        }

        if (product.IsDeleted)
        {
            product.IsDeleted = false;
            product.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private static IQueryable<Product> ApplyFilters(
        IQueryable<Product> query,
        ProductListRequest request)
    {
        query = (request.ActivityStatus ?? ProductActivityStatus.Active) switch
        {
            ProductActivityStatus.Active =>
                query.Where(product => !product.IsDeleted),
            ProductActivityStatus.Deleted =>
                query.Where(product => product.IsDeleted),
            ProductActivityStatus.All => query,
            _ => throw new ArgumentOutOfRangeException(
                nameof(request.ActivityStatus))
        };

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            query = query.Where(product =>
                product.Name.Contains(search) ||
                product.Description.Contains(search) ||
                product.CabysCode.Contains(search));
        }

        if (request.CategoryId.HasValue)
        {
            query = query.Where(product =>
                product.CategoryId == request.CategoryId.Value);
        }

        if (request.UnitId.HasValue)
        {
            query = query.Where(product =>
                product.InventoryUnitId == request.UnitId.Value ||
                product.CommercialUnitId == request.UnitId.Value);
        }

        query = (request.StockStatus ?? ProductStockStatus.All) switch
        {
            ProductStockStatus.All => query,
            ProductStockStatus.InStock =>
                query.Where(product =>
                    product.StockQuantity > product.MinimumStock),
            ProductStockStatus.LowStock =>
                query.Where(product =>
                    product.StockQuantity > 0m &&
                    product.StockQuantity <= product.MinimumStock),
            ProductStockStatus.OutOfStock =>
                query.Where(product => product.StockQuantity == 0m),
            _ => throw new ArgumentOutOfRangeException(
                nameof(request.StockStatus))
        };

        return query;
    }

    private async Task ValidateCatalogReferencesAsync(
        ProductUpsertRequest request,
        Product? currentProduct,
        CancellationToken cancellationToken)
    {
        var categoryIsValid = await dbContext.ProductCategories
            .AsNoTracking()
            .AnyAsync(
                category =>
                    category.Id == request.CategoryId &&
                    (category.IsActive ||
                     currentProduct != null &&
                     currentProduct.CategoryId == category.Id),
                cancellationToken);

        if (!categoryIsValid)
        {
            throw new InvalidProductCatalogReferenceException(
                "La categoría no existe o está inactiva.");
        }

        var validUnitIds = await dbContext.UnitsOfMeasure
            .AsNoTracking()
            .Where(unit =>
                (unit.Id == request.InventoryUnitId ||
                 unit.Id == request.CommercialUnitId) &&
                (unit.IsActive ||
                 currentProduct != null &&
                 (currentProduct.InventoryUnitId == unit.Id ||
                  currentProduct.CommercialUnitId == unit.Id)))
            .Select(unit => unit.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (!validUnitIds.Contains(request.InventoryUnitId) ||
            !validUnitIds.Contains(request.CommercialUnitId))
        {
            throw new InvalidProductCatalogReferenceException(
                "Una de las unidades no existe o está inactiva.");
        }
    }

    private static void ApplyRequest(
        Product product,
        ProductUpsertRequest request)
    {
        product.CategoryId = request.CategoryId;
        product.Name = NormalizeRequired(request.Name);
        product.Description = (request.Description ?? string.Empty).Trim();
        product.CabysCode = NormalizeRequired(request.CabysCode);
        product.InventoryUnitId = request.InventoryUnitId;
        product.CommercialUnitId = request.CommercialUnitId;
        product.CommercialUnitsPerInventoryUnit =
            request.CommercialUnitsPerInventoryUnit;
        product.RequiresWholeInventoryUnits =
            request.RequiresWholeInventoryUnits;
        product.SalePrice = request.SalePrice;
        product.CurrentCost = request.CurrentCost;
        product.TaxRate = request.TaxRate;
        product.MinimumStock = request.MinimumStock;
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
