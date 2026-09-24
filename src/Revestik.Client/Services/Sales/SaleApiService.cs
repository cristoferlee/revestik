using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Revestik.Shared.Common;
using Revestik.Shared.Sales;

namespace Revestik.Client.Services.Sales;

public sealed class SaleApiService(
    HttpClient httpClient)
    : ISaleApiService
{
    public async Task<PaginatedResponse<SaleListItemResponse>>
        GetPageAsync(
            SaleListRequest request,
            CancellationToken cancellationToken = default)
    {
        var url = BuildListUrl(request);

        var response = await httpClient.GetAsync(
            url,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await ReadRequiredAsync<
            PaginatedResponse<SaleListItemResponse>>(
            response,
            "The API returned an empty sales page response.",
            cancellationToken);
    }

    public async Task<SaleSummaryResponse> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            "api/sales/summary",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await ReadRequiredAsync<SaleSummaryResponse>(
            response,
            "The API returned an empty sales summary response.",
            cancellationToken);
    }

    public async Task<SaleResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"api/sales/{id}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<SaleResponse>(
                cancellationToken);
    }

    public async Task<SaleResponse> CreateAsync(
        SaleUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            "api/sales",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await ReadRequiredSaleAsync(
            response,
            cancellationToken);
    }

    public async Task<SaleResponse?> UpdateAsync(
        int id,
        SaleUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"api/sales/{id}",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await ReadRequiredSaleAsync(
            response,
            cancellationToken);
    }

    public async Task<SaleResponse?> CreateFromQuotationAsync(
        int quotationId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync(
            $"api/sales/from-quotation/{quotationId}",
            content: null,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await ReadRequiredSaleAsync(
            response,
            cancellationToken);
    }

    public async Task<SaleResponse?> IssueAsync(
        int id,
        SaleUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"api/sales/{id}/issue",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await ReadRequiredSaleAsync(
            response,
            cancellationToken);
    }

    public async Task<SaleResponse?> VoidAsync(
        int id,
        VoidSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"api/sales/{id}/void",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await ReadRequiredSaleAsync(
            response,
            cancellationToken);
    }

    public async Task<SaleResponse?> CreateReplacementAsync(
        int id,
        VoidSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"api/sales/{id}/replacement",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await ReadRequiredSaleAsync(
            response,
            cancellationToken);
    }

    public async Task<SaleResponse?> RegisterPaymentAsync(
        int id,
        SalePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"api/sales/{id}/payments",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await ReadRequiredSaleAsync(
            response,
            cancellationToken);
    }

    public async Task<SaleResponse?> VoidPaymentAsync(
        int id,
        int paymentId,
        VoidSalePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"api/sales/{id}/payments/{paymentId}/void",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await ReadRequiredSaleAsync(
            response,
            cancellationToken);
    }

    public async Task<byte[]?> GetPdfAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"api/sales/{id}/pdf",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(
            cancellationToken);
    }

    private static string BuildListUrl(
        SaleListRequest request)
    {
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            AddQueryParameter(
                parameters,
                "Search",
                request.Search);
        }

        if (request.DateFromUtc.HasValue)
        {
            AddQueryParameter(
                parameters,
                "DateFromUtc",
                request.DateFromUtc.Value.ToString(
                    "O",
                    CultureInfo.InvariantCulture));
        }

        if (request.DateToUtc.HasValue)
        {
            AddQueryParameter(
                parameters,
                "DateToUtc",
                request.DateToUtc.Value.ToString(
                    "O",
                    CultureInfo.InvariantCulture));
        }

        if (request.Status.HasValue)
        {
            AddQueryParameter(
                parameters,
                "Status",
                request.Status.Value.ToString());
        }

        if (request.BalanceStatus.HasValue)
        {
            AddQueryParameter(
                parameters,
                "BalanceStatus",
                request.BalanceStatus.Value.ToString());
        }

        if (request.Currency.HasValue)
        {
            AddQueryParameter(
                parameters,
                "Currency",
                request.Currency.Value.ToString());
        }

        AddQueryParameter(
            parameters,
            "Page",
            request.Page.ToString(
                CultureInfo.InvariantCulture));

        AddQueryParameter(
            parameters,
            "PageSize",
            request.PageSize.ToString(
                CultureInfo.InvariantCulture));

        return parameters.Count == 0
            ? "api/sales"
            : $"api/sales?{string.Join("&", parameters)}";
    }

    private static void AddQueryParameter(
        ICollection<string> parameters,
        string name,
        string value)
    {
        parameters.Add(
            $"{Uri.EscapeDataString(name)}=" +
            $"{Uri.EscapeDataString(value)}");
    }

    private static Task<SaleResponse> ReadRequiredSaleAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken) =>
        ReadRequiredAsync<SaleResponse>(
            response,
            "The API returned an empty sale response.",
            cancellationToken);

    private static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        string emptyResponseMessage,
        CancellationToken cancellationToken)
    {
        var result = await response.Content
            .ReadFromJsonAsync<T>(
                cancellationToken);

        return result
            ?? throw new InvalidOperationException(
                emptyResponseMessage);
    }
}