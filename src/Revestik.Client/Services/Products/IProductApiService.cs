using Revestik.Shared.Common;
using Revestik.Shared.Inventory;
using Revestik.Shared.Products;

namespace Revestik.Client.Services.Products;

public interface IProductApiService
{
    Task<PaginatedResponse<ProductListItemResponse>> GetPageAsync(
        ProductListRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<ProductResponse> CreateWithInitialStockAsync(
        InventoryProductCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductResponse?> UpdateAsync(
        int id,
        ProductUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<bool> ReactivateAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductCategoryResponse>> GetCategoriesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UnitOfMeasureResponse>> GetUnitsAsync(
        CancellationToken cancellationToken = default);
}