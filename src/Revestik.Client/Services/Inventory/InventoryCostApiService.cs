using System.Net;
using System.Net.Http.Json;
using Revestik.Shared.Inventory;

namespace Revestik.Client.Services.Inventory;

public sealed class InventoryCostApiService(HttpClient httpClient)
    : IInventoryCostApiService
{
    public async Task<IReadOnlyList<UnknownCostLayerResponse>>
        GetUnresolvedAsync(
            CancellationToken cancellationToken = default)
    {
        return await httpClient
            .GetFromJsonAsync<List<UnknownCostLayerResponse>>(
                "api/inventory/unknown-cost-layers",
                cancellationToken)
            ?? [];
    }

    public async Task<ResolvedInventoryCostResponse?> ResolveAsync(
        int layerId,
        ResolveInventoryCostRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"api/inventory/cost-layers/{layerId}/resolve",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<ResolvedInventoryCostResponse>(
                cancellationToken: cancellationToken);
    }
}