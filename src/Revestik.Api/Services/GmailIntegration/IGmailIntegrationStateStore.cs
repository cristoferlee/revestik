namespace Revestik.Api.Services.GmailIntegration;

internal interface IGmailIntegrationStateStore
{
    Task<GmailIntegrationState> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(GmailIntegrationState state, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}
