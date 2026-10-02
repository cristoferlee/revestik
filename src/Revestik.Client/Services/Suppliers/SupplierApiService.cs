using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Revestik.Shared.Common;
using Revestik.Shared.Suppliers;

namespace Revestik.Client.Services.Suppliers;

public sealed class SupplierApiService(HttpClient httpClient)
    : ISupplierApiService
{
    public async Task<PaginatedResponse<SupplierListItemResponse>> GetPageAsync(
        SupplierListRequest request,
        CancellationToken cancellationToken = default)
    {
        var queryParameters = new List<string>
        {
            $"page={request.Page.ToString(CultureInfo.InvariantCulture)}",
            $"pageSize={request.PageSize.ToString(CultureInfo.InvariantCulture)}",
            $"includeInactive={request.IncludeInactive.GetValueOrDefault().ToString().ToLowerInvariant()}"
        };

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            queryParameters.Add(
                $"search={Uri.EscapeDataString(request.Search.Trim())}");
        }

        var requestUri =
            $"api/suppliers?{string.Join("&", queryParameters)}";

        return await httpClient
            .GetFromJsonAsync<PaginatedResponse<SupplierListItemResponse>>(
                requestUri,
                cancellationToken)
            ?? new PaginatedResponse<SupplierListItemResponse>(
                [],
                request.Page,
                request.PageSize,
                0);
    }

    public async Task<SupplierResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"api/suppliers/{id}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SupplierResponse>(
            cancellationToken);
    }

    public async Task<SupplierResponse> CreateAsync(
        SupplierUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            "api/suppliers",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new SupplierNameConflictException();
        }

        response.EnsureSuccessStatusCode();

        return await ReadRequiredSupplierAsync(response, cancellationToken);
    }

    public async Task<SupplierResponse?> UpdateAsync(
        int id,
        SupplierUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"api/suppliers/{id}",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new SupplierNameConflictException();
        }

        response.EnsureSuccessStatusCode();

        return await ReadRequiredSupplierAsync(response, cancellationToken);
    }

    public Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        SendLifecycleAsync(id, "deactivate", cancellationToken);

    public Task<bool> ReactivateAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        SendLifecycleAsync(id, "reactivate", cancellationToken);

    private async Task<bool> SendLifecycleAsync(
        int id,
        string action,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsync(
            $"api/suppliers/{id}/{action}",
            null,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    private static async Task<SupplierResponse> ReadRequiredSupplierAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var supplier = await response.Content
            .ReadFromJsonAsync<SupplierResponse>(cancellationToken);

        return supplier
            ?? throw new InvalidOperationException(
                "The API returned an empty supplier response.");
    }
}