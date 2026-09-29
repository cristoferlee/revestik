using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Revestik.Shared.Common;
using Revestik.Shared.Inventory;
using Revestik.Shared.Products;

namespace Revestik.Client.Services.Products;

public sealed class ProductApiService(
    HttpClient httpClient)
    : IProductApiService
{
    public async Task<PaginatedResponse<ProductListItemResponse>> GetPageAsync(
        ProductListRequest request,
        CancellationToken cancellationToken = default)
    {
        var queryParameters = new List<string>
        {
            $"page={request.Page.ToString(CultureInfo.InvariantCulture)}",
            $"pageSize={request.PageSize.ToString(CultureInfo.InvariantCulture)}"
        };

        if (request.ActivityStatus.HasValue)
        {
            queryParameters.Add(
                $"activityStatus={request.ActivityStatus.Value}");
        }

        if (request.StockStatus.HasValue)
        {
            queryParameters.Add(
                $"stockStatus={request.StockStatus.Value}");
        }

        if (request.SortBy.HasValue)
        {
            queryParameters.Add(
                $"sortBy={request.SortBy.Value}");
        }

        if (request.SortDirection.HasValue)
        {
            queryParameters.Add(
                $"sortDirection={request.SortDirection.Value}");
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            queryParameters.Add(
                $"search={Uri.EscapeDataString(request.Search.Trim())}");
        }

        if (request.CategoryId.HasValue)
        {
            queryParameters.Add(
                $"categoryId={request.CategoryId.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (request.UnitId.HasValue)
        {
            queryParameters.Add(
                $"unitId={request.UnitId.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        var requestUri =
            $"api/products?{string.Join("&", queryParameters)}";

        return await httpClient
            .GetFromJsonAsync<PaginatedResponse<ProductListItemResponse>>(
                requestUri,
                cancellationToken)
            ?? new PaginatedResponse<ProductListItemResponse>(
                [],
                request.Page,
                request.PageSize,
                0);
    }

    public async Task<ProductResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"api/products/{id}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ProductResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<ProductResponse> CreateWithInitialStockAsync(
        InventoryProductCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/products/with-initial-stock",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ProductResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException(
                "El producto creado no pudo cargarse.");
    }

    public async Task<ProductResponse?> UpdateAsync(
        int id,
        ProductUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"api/products/{id}",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ProductResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync(
            $"api/products/{id}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<bool> ReactivateAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync(
            $"api/products/{id}/reactivate",
            content: null,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<IReadOnlyList<ProductCategoryResponse>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        return await httpClient
            .GetFromJsonAsync<List<ProductCategoryResponse>>(
                "api/product-categories",
                cancellationToken)
            ?? [];
    }

    public async Task<IReadOnlyList<UnitOfMeasureResponse>> GetUnitsAsync(
        CancellationToken cancellationToken = default)
    {
        return await httpClient
            .GetFromJsonAsync<List<UnitOfMeasureResponse>>(
                "api/units-of-measure",
                cancellationToken)
            ?? [];
    }
}