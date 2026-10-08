using Revestik.Shared.Integrations.Gmail;

namespace Revestik.Api.Services.GmailIntegration;

public interface IBankVoucherGmailIntegrationService
{
    Task<GmailMailboxStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken);

    string CreateAuthorizationUrl(string userId);

    Task CompleteAuthorizationAsync(
        string code,
        string state,
        string userId,
        CancellationToken cancellationToken);

    Task<BankVoucherGmailSyncResultResponse> SyncAsync(
        CancellationToken cancellationToken);

    Task DisconnectAsync(CancellationToken cancellationToken);
}
