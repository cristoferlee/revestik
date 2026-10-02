using Revestik.Api.Authorization;
using Revestik.Api.Services.Inventory;

namespace Revestik.Api.Endpoints;

public static class InventoryCostLayerEndpoints
{
    public static IEndpointRouteBuilder MapInventoryCostLayerEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/inventory/cost-layers",
                async (
                    int? productId,
                    IInventoryCostResolutionService service,
                    CancellationToken cancellationToken) =>
                {
                    if (productId is <= 0)
                    {
                        return Results.BadRequest();
                    }

                    var layers = await service.GetLayersAsync(
                        productId,
                        cancellationToken);

                    return Results.Ok(layers);
                })
            .WithName("GetInventoryCostLayers")
            .WithTags("Inventory")
            .RequireAuthorization(PolicyNames.ManageInventory);

        return endpoints;
    }
}