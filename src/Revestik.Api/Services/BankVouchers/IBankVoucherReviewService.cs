using Revestik.Shared.Expenses;

namespace Revestik.Api.Services.BankVouchers;

public interface IBankVoucherReviewService
{
    Task<IReadOnlyList<BankVoucherReviewItemResponse>> GetAsync(
        BankVoucherListRequest request,
        CancellationToken cancellationToken);

    Task<BankVoucherReviewItemResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<bool> AcceptAsync(
        int id,
        CancellationToken cancellationToken);

    Task<bool> IgnoreAsync(
        int id,
        CancellationToken cancellationToken);

    Task<bool> MatchAsync(
        int id,
        int electronicDocumentId,
        CancellationToken cancellationToken);
}
