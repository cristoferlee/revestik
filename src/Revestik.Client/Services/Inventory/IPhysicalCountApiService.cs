using Revestik.Shared.Inventory;

namespace Revestik.Client.Services.Inventory;

public interface IPhysicalCountApiService
{
    Task<PhysicalCountResponse> StartAsync(
        PhysicalCountCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<PhysicalCountResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<PhysicalCountResponse?> UpdateLinesAsync(
        int id,
        PhysicalCountUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<PhysicalCountResponse?> CompleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
