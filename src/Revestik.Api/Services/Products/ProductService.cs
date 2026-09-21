using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Shared.Common;
using Revestik.Shared.Products;

namespace Revestik.Api.Services.Products;

public sealed class ProductService(
    RevestikDbContext dbContext)
    : IProductService
{
    public async Task<PaginatedResponse<ProductListItemResponse>> GetPageAsync(
        ProductListRequest request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Products
            .AsNoTracking()
            .Where(product => !product.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var normalizedSearch = request.Search.Trim();

            query = query.Where(product =>
                product.Description.Contains(normalizedSearch) ||
                (product.CabysCode != null &&
                 product.CabysCode.Contains(normalizedSearch)));
        }

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
                .OrderBy(product => product.Description)
                .ThenBy(product => product.Id)
                .Skip((int)skip)
                .Take(request.PageSize)
                .Select(product => new ProductListItemResponse(
                    product.Id,
                    product.Description,
                    product.CabysCode ?? string.Empty,
                    product.Unit,
                    product.SalePrice,
                    product.TaxRate,
                    product.StockQuantity))
                .ToListAsync(cancellationToken);
        }

        return new PaginatedResponse<ProductListItemResponse>(
            items,
            request.Page,
            request.PageSize,
            totalCount);
    }
}