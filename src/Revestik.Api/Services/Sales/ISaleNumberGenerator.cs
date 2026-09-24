namespace Revestik.Api.Services.Sales;

public interface ISaleNumberGenerator
{
    Task<string> GenerateAsync(
        CancellationToken cancellationToken);
}