using Revestik.Shared.Products;

namespace Revestik.Api.Services.Products;

public interface IInventoryCatalogService
{
    Task<IReadOnlyList<ProductCategoryResponse>> GetCategoriesAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ProductCategoryResponse?> GetCategoryByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<ProductCategoryResponse> CreateCategoryAsync(
        ProductCategoryUpsertRequest request,
        CancellationToken cancellationToken);

    Task<ProductCategoryResponse?> UpdateCategoryAsync(
        int id,
        ProductCategoryUpsertRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeactivateCategoryAsync(
        int id,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductSubcategoryResponse>> GetSubcategoriesAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ProductSubcategoryResponse?> GetSubcategoryByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<ProductSubcategoryResponse> CreateSubcategoryAsync(
        ProductSubcategoryUpsertRequest request,
        CancellationToken cancellationToken);

    Task<ProductSubcategoryResponse?> UpdateSubcategoryAsync(
        int id,
        ProductSubcategoryUpsertRequest request,
        string version,
        CancellationToken cancellationToken);

    Task<bool> DeactivateSubcategoryAsync(
        int id,
        string version,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UnitOfMeasureResponse>> GetUnitsAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<UnitOfMeasureResponse?> GetUnitByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<UnitOfMeasureResponse> CreateUnitAsync(
        UnitOfMeasureUpsertRequest request,
        CancellationToken cancellationToken);

    Task<UnitOfMeasureResponse?> UpdateUnitAsync(
        int id,
        UnitOfMeasureUpsertRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeactivateUnitAsync(
        int id,
        CancellationToken cancellationToken);
}