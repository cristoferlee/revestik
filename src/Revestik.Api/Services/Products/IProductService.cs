using Revestik.Shared.Common;
using Revestik.Shared.Products;

namespace Revestik.Api.Services.Products;

public interface IProductService
{
    Task<PaginatedResponse<ProductListItemResponse>> GetPageAsync(
        ProductListRequest request,
        CancellationToken cancellationToken);
}