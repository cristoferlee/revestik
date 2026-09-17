namespace Revestik.Api.Services.Quotations;

public interface IQuotationNumberGenerator
{
    Task<string> GenerateAsync(
        CancellationToken cancellationToken);
}