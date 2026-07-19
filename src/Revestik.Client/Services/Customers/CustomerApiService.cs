using System.Net;
using System.Net.Http.Json;
using Revestik.Shared.Customers;

namespace Revestik.Client.Services.Customers;

public sealed class CustomerApiService(
    HttpClient httpClient)
    : ICustomerApiService
{
    public async Task<IReadOnlyList<CustomerResponse>> GetAllAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        var requestUri = string.IsNullOrWhiteSpace(search)
            ? "api/customers"
            : $"api/customers?search={Uri.EscapeDataString(search.Trim())}";

        return await httpClient
            .GetFromJsonAsync<List<CustomerResponse>>(
                requestUri,
                cancellationToken) ?? [];
    }

    public async Task<CustomerResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"api/customers/{id}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<CustomerResponse>(
                cancellationToken);
    }

    public async Task<CustomerResponse> CreateAsync(
        CustomerUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            "api/customers",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await ReadRequiredCustomerAsync(
            response,
            cancellationToken);
    }

    public async Task<CustomerResponse?> UpdateAsync(
        int id,
        CustomerUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"api/customers/{id}",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await ReadRequiredCustomerAsync(
            response,
            cancellationToken);
    }

    public async Task<bool> DeactivateAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync(
            $"api/customers/{id}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();

        return true;
    }

    private static async Task<CustomerResponse> ReadRequiredCustomerAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var customer = await response.Content
            .ReadFromJsonAsync<CustomerResponse>(
                cancellationToken);

        return customer
            ?? throw new InvalidOperationException(
                "The API returned an empty customer response.");
    }
}