namespace Revestik.Api.Integrations.Hacienda;

public interface IHaciendaTaxpayerClient
{
    Task<TaxpayerLookupResult> FindAsync(
        string identificationNumber,
        CancellationToken cancellationToken = default);
}