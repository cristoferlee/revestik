using Revestik.Shared.Inventory;

namespace Revestik.Api.Services.Inventory;

public interface IInventoryPhysicalCountService
{
    Task<PhysicalCountResponse> StartAsync(
        PhysicalCountCreateRequest request,
        string startedByUserId,
        CancellationToken cancellationToken);

    Task<PhysicalCountResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<PhysicalCountResponse?> UpdateLinesAsync(
        int id,
        PhysicalCountUpdateRequest request,
        string updatedByUserId,
        CancellationToken cancellationToken);

    Task<PhysicalCountResponse?> CompleteAsync(
        int id,
        string completedByUserId,
        CancellationToken cancellationToken);
}