using Revestik.Shared.Taxpayers;

namespace Revestik.Client.Services.Taxpayers;

public enum TaxpayerApiLookupStatus
{
    Found,
    NotFound,
    RateLimited,
    Unavailable
}

public sealed record TaxpayerApiLookupResult(
    TaxpayerApiLookupStatus Status,
    TaxpayerLookupResponse? Taxpayer = null);

public interface ITaxpayerApiService
{
    Task<TaxpayerApiLookupResult> FindAsync(
        string identificationNumber,
        CancellationToken cancellationToken = default);
}