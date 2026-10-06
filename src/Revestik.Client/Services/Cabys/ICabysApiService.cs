using Revestik.Shared.Cabys;
using Revestik.Shared.Common;

namespace Revestik.Client.Services.Cabys;

public interface ICabysApiService
{
    Task<CabysCatalogStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken = default);

    Task<CabysCatalogLoadResponse> LoadBundledCatalogAsync(
        CancellationToken cancellationToken = default);

    Task<PaginatedResponse<CabysListItemResponse>> SearchAsync(
        CabysSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<CabysDetailResponse?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);
}
