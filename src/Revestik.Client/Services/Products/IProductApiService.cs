using Revestik.Shared.Common;
using Revestik.Shared.Products;

namespace Revestik.Client.Services.Products;

public interface IProductApiService
{
    Task<PaginatedResponse<ProductListItemResponse>> GetPageAsync(
        ProductListRequest request,
        CancellationToken cancellationToken = default);
}