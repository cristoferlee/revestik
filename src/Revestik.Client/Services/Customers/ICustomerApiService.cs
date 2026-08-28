using Revestik.Shared.Common;
using Revestik.Shared.Customers;

namespace Revestik.Client.Services.Customers;

public interface ICustomerApiService
{
    Task<PaginatedResponse<CustomerListItemResponse>> GetPageAsync(
        CustomerListRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<CustomerResponse> CreateAsync(
        CustomerUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerResponse?> UpdateAsync(
        int id,
        CustomerUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken = default);
}