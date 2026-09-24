using Revestik.Shared.Common;
using Revestik.Shared.Quotations;

namespace Revestik.Api.Services.Quotations;

public interface IQuotationService
{
    Task<PaginatedResponse<QuotationListItemResponse>> GetPageAsync(
        QuotationListRequest request,
        CancellationToken cancellationToken);

    Task<QuotationResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<QuotationResponse> CreateAsync(
        QuotationUpsertRequest request,
        string createdByUserId,
        CancellationToken cancellationToken);

    Task<QuotationResponse?> UpdateAsync(
        int id,
        QuotationUpsertRequest request,
        CancellationToken cancellationToken);

    Task<QuotationResponse?> IssueAsync(
        int id,
        QuotationUpsertRequest request,
        CancellationToken cancellationToken);
}