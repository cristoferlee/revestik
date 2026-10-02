using Revestik.Shared.Common;
using Revestik.Shared.Suppliers;

namespace Revestik.Api.Services.Suppliers;

public interface ISupplierService
{
    Task<PaginatedResponse<SupplierListItemResponse>> GetPageAsync(
        SupplierListRequest request,
        CancellationToken cancellationToken);

    Task<SupplierResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<SupplierResponse> CreateAsync(
        SupplierUpsertRequest request,
        CancellationToken cancellationToken);

    Task<SupplierResponse?> UpdateAsync(
        int id,
        SupplierUpsertRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken);

    Task<bool> ReactivateAsync(
        int id,
        CancellationToken cancellationToken);
}