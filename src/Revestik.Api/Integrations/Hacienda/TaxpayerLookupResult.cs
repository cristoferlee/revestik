namespace Revestik.Api.Integrations.Hacienda;

public enum TaxpayerLookupStatus
{
    Found,
    NotFound,
    RateLimited,
    Unavailable
}

public sealed record TaxpayerLookupResult(
    TaxpayerLookupStatus Status,
    HaciendaTaxpayer? Taxpayer = null);