using System.Net.Http.Json;
using Revestik.Shared.Inventory;

namespace Revestik.Client.Services.Inventory;

public sealed class InventoryApiService(HttpClient httpClient)
    : IInventoryApiService
{
    public async Task<InventorySummaryResponse> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<InventorySummaryResponse>(
            "api/inventory/summary",
            cancellationToken)
            ?? new InventorySummaryResponse(
                0,
                0,
                0,
                0,
                0m,
                0m,
                0);
    }

    public async Task<InventoryMovementResponse?> RegisterInitialStockAsync(
        int productId,
        InitialStockRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"api/inventory/products/{productId}/initial-stock",
            request,
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<InventoryMovementResponse>(
                cancellationToken: cancellationToken);
    }

    public async Task<InventoryMovementResponse?> AdjustStockAsync(
        int productId,
        InventoryAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"api/inventory/products/{productId}/adjustments",
            request,
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<InventoryMovementResponse>(
                cancellationToken: cancellationToken);
    }
}