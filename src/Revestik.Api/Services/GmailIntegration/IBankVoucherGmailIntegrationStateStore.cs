namespace Revestik.Api.Services.GmailIntegration;

internal interface IBankVoucherGmailIntegrationStateStore
{
    Task<GmailIntegrationState> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(GmailIntegrationState state, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}
