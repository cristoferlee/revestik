using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Client.Services.ElectronicDocuments;

public interface IGmailIntegrationApiService
{
    Task<GmailIntegrationStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken = default);

    Task<string> GetAuthorizationUrlAsync(
        CancellationToken cancellationToken = default);

    Task<GmailSyncResultResponse> SyncAsync(
        CancellationToken cancellationToken = default);

    Task DisconnectAsync(
        CancellationToken cancellationToken = default);
}
