using Revestik.Shared.Quotations;

namespace Revestik.Api.Services.Quotations;

public interface IQuotationService
{
    Task<QuotationResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<QuotationResponse> CreateAsync(
        QuotationUpsertRequest request,
        CancellationToken cancellationToken);

    Task<QuotationResponse?> UpdateAsync(
        int id,
        QuotationUpsertRequest request,
        CancellationToken cancellationToken);
}