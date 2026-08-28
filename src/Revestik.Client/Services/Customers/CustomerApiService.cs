using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Revestik.Shared.Common;
using Revestik.Shared.Customers;

namespace Revestik.Client.Services.Customers;

public sealed class CustomerApiService(
    HttpClient httpClient)
    : ICustomerApiService
{
    public async Task<PaginatedResponse<CustomerListItemResponse>> GetPageAsync(
        CustomerListRequest request,
        CancellationToken cancellationToken = default)
    {
        var queryParameters = new List<string>
        {
            $"page={request.Page.ToString(CultureInfo.InvariantCulture)}",
            $"pageSize={request.PageSize.ToString(CultureInfo.InvariantCulture)}"
        };

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            queryParameters.Add(
                $"search={Uri.EscapeDataString(request.Search.Trim())}");
        }

        if (request.IdentificationType.HasValue)
        {
            queryParameters.Add(
                "identificationType=" +
                Uri.EscapeDataString(
                    request.IdentificationType.Value.ToString()));
        }

        var requestUri =
            $"api/customers?{string.Join("&", queryParameters)}";

        return await httpClient
            .GetFromJsonAsync<
                PaginatedResponse<CustomerListItemResponse>>(
                requestUri,
                cancellationToken)
            ?? new PaginatedResponse<CustomerListItemResponse>(
                [],
                request.Page,
                request.PageSize,
                0);
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

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new CustomerIdentificationConflictException();
        }

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

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new CustomerIdentificationConflictException();
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