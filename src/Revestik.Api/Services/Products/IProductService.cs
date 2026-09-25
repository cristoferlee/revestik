using Revestik.Shared.Common;
using Revestik.Shared.Products;

namespace Revestik.Api.Services.Products;

public interface IProductService
{
    Task<PaginatedResponse<ProductListItemResponse>> GetPageAsync(
        ProductListRequest request,
        CancellationToken cancellationToken);

    Task<ProductResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<ProductResponse> CreateAsync(
        ProductUpsertRequest request,
        CancellationToken cancellationToken);

    Task<ProductResponse?> UpdateAsync(
        int id,
        ProductUpsertRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken);

    Task<bool> ReactivateAsync(
        int id,
        CancellationToken cancellationToken);
}