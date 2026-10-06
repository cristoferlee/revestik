using Revestik.Shared.Cabys;
using Revestik.Shared.Common;

namespace Revestik.Api.Services.Cabys;

public interface ICabysCatalogService
{
    Task<CabysCatalogStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken);

    Task<CabysCatalogLoadResponse> LoadBundledCatalogAsync(
        CancellationToken cancellationToken);

    Task<PaginatedResponse<CabysListItemResponse>> SearchAsync(
        CabysSearchRequest request,
        CancellationToken cancellationToken);

    Task<CabysDetailResponse?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken);
}
