using Revestik.Shared.Inventory;

namespace Revestik.Api.Services.Inventory;

public interface IInventoryCostResolutionService
{
    Task<IReadOnlyList<UnknownCostLayerResponse>> GetUnresolvedAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<InventoryCostLayerResponse>> GetLayersAsync(
        int? productId,
        CancellationToken cancellationToken);

    Task<ResolvedInventoryCostResponse?> ResolveAsync(
        int layerId,
        ResolveInventoryCostRequest request,
        string resolvedByUserId,
        CancellationToken cancellationToken);
}