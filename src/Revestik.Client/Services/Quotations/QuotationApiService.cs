using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Revestik.Shared.Common;
using Revestik.Shared.Quotations;

namespace Revestik.Client.Services.Quotations;

public sealed class QuotationApiService(
    HttpClient httpClient)
    : IQuotationApiService
{
    public async Task<
        PaginatedResponse<QuotationListItemResponse>>
        GetPageAsync(
            QuotationListRequest request,
            CancellationToken cancellationToken = default)
    {
        var url =
            BuildListUrl(request);

        var response =
            await httpClient.GetAsync(
                url,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        return await ReadRequiredAsync<
            PaginatedResponse<QuotationListItemResponse>>(
            response,
            "The API returned an empty quotation page response.",
            cancellationToken);
    }

    public async Task<QuotationResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var response =
            await httpClient.GetAsync(
                $"api/quotations/{id}",
                cancellationToken);

        if (response.StatusCode ==
            HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<QuotationResponse>(
                cancellationToken);
    }

    public async Task<QuotationResponse> CreateAsync(
        QuotationUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var response =
            await httpClient.PostAsJsonAsync(
                "api/quotations",
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        return await ReadRequiredQuotationAsync(
            response,
            cancellationToken);
    }

    public async Task<QuotationResponse?> UpdateAsync(
        int id,
        QuotationUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var response =
            await httpClient.PutAsJsonAsync(
                $"api/quotations/{id}",
                request,
                cancellationToken);

        if (response.StatusCode ==
            HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await ReadRequiredQuotationAsync(
            response,
            cancellationToken);
    }

    public async Task<QuotationResponse?> IssueAsync(
        int id,
        QuotationUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var response =
            await httpClient.PostAsJsonAsync(
                $"api/quotations/{id}/issue",
                request,
                cancellationToken);

        if (response.StatusCode ==
            HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await ReadRequiredQuotationAsync(
            response,
            cancellationToken);
    }

    public async Task<byte[]?> GetPdfAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var response =
            await httpClient.GetAsync(
                $"api/quotations/{id}/pdf",
                cancellationToken);

        if (response.StatusCode ==
            HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadAsByteArrayAsync(
                cancellationToken);
    }

    private static string BuildListUrl(
        QuotationListRequest request)
    {
        var parameters =
            new List<string>();

        if (!string.IsNullOrWhiteSpace(
                request.Search))
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

        return $"api/quotations?{string.Join(
            "&",
            parameters)}";
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

    private static Task<QuotationResponse>
        ReadRequiredQuotationAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken) =>
        ReadRequiredAsync<QuotationResponse>(
            response,
            "The API returned an empty quotation response.",
            cancellationToken);

    private static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        string emptyResponseMessage,
        CancellationToken cancellationToken)
    {
        var result =
            await response.Content
                .ReadFromJsonAsync<T>(
                    cancellationToken);

        return result
            ?? throw new InvalidOperationException(
                emptyResponseMessage);
    }
}