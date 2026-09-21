using System.Globalization;
using System.Net.Http.Json;
using Revestik.Shared.Common;
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

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            queryParameters.Add(
                $"search={Uri.EscapeDataString(request.Search.Trim())}");
        }

        var requestUri =
            $"api/products?{string.Join("&", queryParameters)}";

        return await httpClient
            .GetFromJsonAsync<
                PaginatedResponse<ProductListItemResponse>>(
                requestUri,
                cancellationToken)
            ?? new PaginatedResponse<ProductListItemResponse>(
                [],
                request.Page,
                request.PageSize,
                0);
    }
}