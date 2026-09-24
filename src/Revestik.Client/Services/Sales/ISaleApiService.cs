using Revestik.Shared.Common;
using Revestik.Shared.Sales;

namespace Revestik.Client.Services.Sales;

public interface ISaleApiService
{
    Task<PaginatedResponse<SaleListItemResponse>> GetPageAsync(
        SaleListRequest request,
        CancellationToken cancellationToken = default);

    Task<SaleSummaryResponse> GetSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<SaleResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<SaleResponse> CreateAsync(
        SaleUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<SaleResponse?> UpdateAsync(
        int id,
        SaleUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<SaleResponse?> CreateFromQuotationAsync(
        int quotationId,
        CancellationToken cancellationToken = default);

    Task<SaleResponse?> IssueAsync(
        int id,
        SaleUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<SaleResponse?> VoidAsync(
        int id,
        VoidSaleRequest request,
        CancellationToken cancellationToken = default);

    Task<SaleResponse?> CreateReplacementAsync(
        int id,
        VoidSaleRequest request,
        CancellationToken cancellationToken = default);

    Task<SaleResponse?> RegisterPaymentAsync(
        int id,
        SalePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<SaleResponse?> VoidPaymentAsync(
        int id,
        int paymentId,
        VoidSalePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<byte[]?> GetPdfAsync(
        int id,
        CancellationToken cancellationToken = default);
}