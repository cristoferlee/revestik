using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.GmailIntegration;

public interface IGmailIntegrationService
{
    Task<GmailIntegrationStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken);

    string CreateAuthorizationUrl(string userId);

    Task CompleteAuthorizationAsync(
        string code,
        string state,
        string userId,
        CancellationToken cancellationToken);

    Task<GmailSyncResultResponse> SyncAsync(
        CancellationToken cancellationToken);

    Task DisconnectAsync(
        CancellationToken cancellationToken);
}
