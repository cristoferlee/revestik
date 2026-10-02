using Revestik.Shared.Inventory;

namespace Revestik.Client.Services.Inventory;

public interface IInventoryCostApiService
{
    Task<IReadOnlyList<UnknownCostLayerResponse>> GetUnresolvedAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryCostLayerResponse>> GetLayersAsync(
        int? productId = null,
        CancellationToken cancellationToken = default);

    Task<ResolvedInventoryCostResponse?> ResolveAsync(
        int layerId,
        ResolveInventoryCostRequest request,
        CancellationToken cancellationToken = default);
}