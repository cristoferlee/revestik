using Revestik.Shared.Common;
using Revestik.Shared.Sales;

namespace Revestik.Api.Services.Sales;

public interface ISaleService
{
    Task<PaginatedResponse<SaleListItemResponse>> GetPageAsync(
        SaleListRequest request,
        CancellationToken cancellationToken);

    Task<SaleSummaryResponse> GetSummaryAsync(
        CancellationToken cancellationToken);

    Task<SaleResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<SaleResponse> CreateAsync(
        SaleUpsertRequest request,
        string createdByUserId,
        CancellationToken cancellationToken);

    Task<SaleResponse?> CreateFromQuotationAsync(
        int quotationId,
        string createdByUserId,
        CancellationToken cancellationToken);

    Task<SaleResponse?> UpdateDraftAsync(
        int id,
        SaleUpsertRequest request,
        CancellationToken cancellationToken);

    Task<SaleResponse?> IssueAsync(
        int id,
        SaleUpsertRequest request,
        string issuedByUserId,
        CancellationToken cancellationToken);

    Task<SaleResponse?> VoidAsync(
        int id,
        VoidSaleRequest request,
        string voidedByUserId,
        CancellationToken cancellationToken);

    Task<SaleResponse?> CreateReplacementAsync(
        int id,
        VoidSaleRequest request,
        string createdByUserId,
        CancellationToken cancellationToken);

    Task<SaleResponse?> RegisterPaymentAsync(
        int id,
        SalePaymentRequest request,
        string createdByUserId,
        CancellationToken cancellationToken);

    Task<SaleResponse?> VoidPaymentAsync(
        int id,
        int paymentId,
        VoidSalePaymentRequest request,
        string voidedByUserId,
        CancellationToken cancellationToken);
}