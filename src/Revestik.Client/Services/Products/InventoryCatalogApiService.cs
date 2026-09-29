using System.Net;
using System.Net.Http.Json;
using Revestik.Shared.Products;

namespace Revestik.Client.Services.Products;

public sealed class InventoryCatalogApiService(HttpClient httpClient)
    : IInventoryCatalogApiService
{
    public async Task<IReadOnlyList<ProductCategoryResponse>> GetCategoriesAsync(
        bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<ProductCategoryResponse>>(
            $"api/product-categories?includeInactive={includeInactive.ToString().ToLowerInvariant()}",
            cancellationToken)
            ?? [];
    }

    public async Task<ProductCategoryResponse> CreateCategoryAsync(
        ProductCategoryUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/product-categories",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ProductCategoryResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException(
                "La categoría creada no pudo cargarse.");
    }

    public async Task<ProductCategoryResponse?> UpdateCategoryAsync(
        int id,
        ProductCategoryUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"api/product-categories/{id}",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ProductCategoryResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<bool> DeactivateCategoryAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync(
            $"api/product-categories/{id}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<IReadOnlyList<UnitOfMeasureResponse>> GetUnitsAsync(
        bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<UnitOfMeasureResponse>>(
            $"api/units-of-measure?includeInactive={includeInactive.ToString().ToLowerInvariant()}",
            cancellationToken)
            ?? [];
    }

    public async Task<UnitOfMeasureResponse> CreateUnitAsync(
        UnitOfMeasureUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/units-of-measure",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<UnitOfMeasureResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException(
                "La unidad creada no pudo cargarse.");
    }

    public async Task<UnitOfMeasureResponse?> UpdateUnitAsync(
        int id,
        UnitOfMeasureUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"api/units-of-measure/{id}",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<UnitOfMeasureResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<bool> DeactivateUnitAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync(
            $"api/units-of-measure/{id}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }
}