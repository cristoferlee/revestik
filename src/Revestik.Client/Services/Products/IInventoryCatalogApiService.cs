using Revestik.Shared.Products;

namespace Revestik.Client.Services.Products;

public interface IInventoryCatalogApiService
{
    Task<IReadOnlyList<ProductCategoryResponse>> GetCategoriesAsync(
        bool includeInactive = true,
        CancellationToken cancellationToken = default);

    Task<ProductCategoryResponse> CreateCategoryAsync(
        ProductCategoryUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductCategoryResponse?> UpdateCategoryAsync(
        int id,
        ProductCategoryUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeactivateCategoryAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductSubcategoryResponse>> GetSubcategoriesAsync(
        bool includeInactive = true, CancellationToken cancellationToken = default);
    Task<ProductSubcategoryResponse> CreateSubcategoryAsync(
        ProductSubcategoryUpsertRequest request, CancellationToken cancellationToken = default);
    Task<ProductSubcategoryResponse?> UpdateSubcategoryAsync(
        int id, ProductSubcategoryWriteRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeactivateSubcategoryAsync(
        int id, string version, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UnitOfMeasureResponse>> GetUnitsAsync(
        bool includeInactive = true,
        CancellationToken cancellationToken = default);

    Task<UnitOfMeasureResponse> CreateUnitAsync(
        UnitOfMeasureUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<UnitOfMeasureResponse?> UpdateUnitAsync(
        int id,
        UnitOfMeasureUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeactivateUnitAsync(
        int id,
        CancellationToken cancellationToken = default);
}