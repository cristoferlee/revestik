using Revestik.Shared.Expenses;
using Revestik.Shared.Integrations.Gmail;

namespace Revestik.Client.Services.Expenses;

public interface IBankVoucherApiService
{
    Task<GmailMailboxStatusResponse> GetGmailStatusAsync(
        CancellationToken cancellationToken = default);

    Task<string> GetAuthorizationUrlAsync(
        CancellationToken cancellationToken = default);

    Task<BankVoucherGmailSyncResultResponse> SyncAsync(
        CancellationToken cancellationToken = default);

    Task DisconnectAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BankVoucherReviewItemResponse>> GetVouchersAsync(
        BankVoucherListRequest request,
        CancellationToken cancellationToken = default);

    Task AcceptAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task IgnoreAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task MatchAsync(
        int id,
        int electronicDocumentId,
        CancellationToken cancellationToken = default);
}
