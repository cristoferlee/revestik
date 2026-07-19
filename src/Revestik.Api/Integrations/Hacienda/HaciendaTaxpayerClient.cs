using System.Net;
using System.Net.Http.Json;

namespace Revestik.Api.Integrations.Hacienda;

public sealed class HaciendaTaxpayerClient(
    HttpClient httpClient,
    ILogger<HaciendaTaxpayerClient> logger)
    : IHaciendaTaxpayerClient
{
    public async Task<TaxpayerLookupResult> FindAsync(
        string identificationNumber,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                $"fe/ae?identificacion={Uri.EscapeDataString(identificationNumber)}",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new TaxpayerLookupResult(
                    TaxpayerLookupStatus.NotFound);
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.LogWarning(
                    "Hacienda rate limit reached while looking up a taxpayer.");

                return new TaxpayerLookupResult(
                    TaxpayerLookupStatus.RateLimited);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Hacienda taxpayer lookup returned status code {StatusCode}.",
                    response.StatusCode);

                return new TaxpayerLookupResult(
                    TaxpayerLookupStatus.Unavailable);
            }

            var taxpayer = await response.Content
                .ReadFromJsonAsync<HaciendaTaxpayer>(
                    cancellationToken);

            if (taxpayer is null ||
                string.IsNullOrWhiteSpace(taxpayer.Name))
            {
                return new TaxpayerLookupResult(
                    TaxpayerLookupStatus.NotFound);
            }

            return new TaxpayerLookupResult(
                TaxpayerLookupStatus.Found,
                taxpayer);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Hacienda taxpayer lookup timed out.");

            return new TaxpayerLookupResult(
                TaxpayerLookupStatus.Unavailable);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Hacienda taxpayer lookup failed.");

            return new TaxpayerLookupResult(
                TaxpayerLookupStatus.Unavailable);
        }
    }
}