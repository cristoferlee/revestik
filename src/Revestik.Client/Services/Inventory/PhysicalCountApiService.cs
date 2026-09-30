using System.Net;
using System.Net.Http.Json;
using Revestik.Shared.Inventory;

namespace Revestik.Client.Services.Inventory;

public sealed class PhysicalCountApiService(HttpClient httpClient)
    : IPhysicalCountApiService
{
    public async Task<PhysicalCountResponse> StartAsync(
        PhysicalCountCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/inventory/physical-counts",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<PhysicalCountResponse>(
                cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException(
                "The physical count response was empty.");
    }

    public async Task<PhysicalCountResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"api/inventory/physical-counts/{id}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<PhysicalCountResponse>(
                cancellationToken: cancellationToken);
    }

    public async Task<PhysicalCountResponse?> UpdateLinesAsync(
        int id,
        PhysicalCountUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"api/inventory/physical-counts/{id}/lines",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<PhysicalCountResponse>(
                cancellationToken: cancellationToken);
    }

    public async Task<PhysicalCountResponse?> CompleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync(
            $"api/inventory/physical-counts/{id}/complete",
            content: null,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<PhysicalCountResponse>(
                cancellationToken: cancellationToken);
    }
}
