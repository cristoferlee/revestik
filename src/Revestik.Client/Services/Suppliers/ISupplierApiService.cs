using Revestik.Shared.Common;
using Revestik.Shared.Suppliers;

namespace Revestik.Client.Services.Suppliers;

public interface ISupplierApiService
{
    Task<PaginatedResponse<SupplierListItemResponse>> GetPageAsync(
        SupplierListRequest request,
        CancellationToken cancellationToken = default);

    Task<SupplierResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<SupplierResponse> CreateAsync(
        SupplierUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<SupplierResponse?> UpdateAsync(
        int id,
        SupplierUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<bool> ReactivateAsync(
        int id,
        CancellationToken cancellationToken = default);
}