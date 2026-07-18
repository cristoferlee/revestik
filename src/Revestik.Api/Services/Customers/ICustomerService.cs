using Revestik.Shared.Customers;

namespace Revestik.Api.Services.Customers;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerResponse>> GetAllAsync(
        string? search,
        CancellationToken cancellationToken);

    Task<CustomerResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<CustomerResponse> CreateAsync(
        CustomerUpsertRequest request,
        CancellationToken cancellationToken);

    Task<CustomerResponse?> UpdateAsync(
        int id,
        CustomerUpsertRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken);
}