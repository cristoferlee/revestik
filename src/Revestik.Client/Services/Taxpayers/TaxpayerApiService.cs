using System.Net;
using System.Net.Http.Json;
using Revestik.Shared.Taxpayers;

namespace Revestik.Client.Services.Taxpayers;

public sealed class TaxpayerApiService(HttpClient httpClient)
    : ITaxpayerApiService
{
    public async Task<TaxpayerApiLookupResult> FindAsync(
        string identificationNumber,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"api/taxpayers/{Uri.EscapeDataString(identificationNumber)}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new TaxpayerApiLookupResult(
                TaxpayerApiLookupStatus.NotFound);
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return new TaxpayerApiLookupResult(
                TaxpayerApiLookupStatus.RateLimited);
        }

        if (!response.IsSuccessStatusCode)
        {
            return new TaxpayerApiLookupResult(
                TaxpayerApiLookupStatus.Unavailable);
        }

        var taxpayer = await response.Content
            .ReadFromJsonAsync<TaxpayerLookupResponse>(
                cancellationToken);

        return taxpayer is null
            ? new TaxpayerApiLookupResult(
                TaxpayerApiLookupStatus.Unavailable)
            : new TaxpayerApiLookupResult(
                TaxpayerApiLookupStatus.Found,
                taxpayer);
    }
}