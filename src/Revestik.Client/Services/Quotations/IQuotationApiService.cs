using Revestik.Shared.Quotations;

namespace Revestik.Client.Services.Quotations;

public interface IQuotationApiService
{
    Task<QuotationResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<QuotationResponse> CreateAsync(
        QuotationUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<QuotationResponse?> UpdateAsync(
        int id,
        QuotationUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<QuotationResponse?> IssueAsync(
        int id,
        QuotationUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<byte[]?> GetPdfAsync(
        int id,
        CancellationToken cancellationToken = default);
}