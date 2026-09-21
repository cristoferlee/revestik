using System.Net;
using System.Net.Http.Json;
using Revestik.Shared.Quotations;

namespace Revestik.Client.Services.Quotations;

public sealed class QuotationApiService(
    HttpClient httpClient)
    : IQuotationApiService
{
    public async Task<QuotationResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"api/quotations/{id}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
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
        var response = await httpClient.PostAsJsonAsync(
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
        var response = await httpClient.PutAsJsonAsync(
            $"api/quotations/{id}",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
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
        var response = await httpClient.PostAsJsonAsync(
            $"api/quotations/{id}/issue",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
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
        var response = await httpClient.GetAsync(
            $"api/quotations/{id}/pdf",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(
            cancellationToken);
    }

    private static async Task<QuotationResponse>
        ReadRequiredQuotationAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
    {
        var quotation = await response.Content
            .ReadFromJsonAsync<QuotationResponse>(
                cancellationToken);

        return quotation
            ?? throw new InvalidOperationException(
                "The API returned an empty quotation response.");
    }
}