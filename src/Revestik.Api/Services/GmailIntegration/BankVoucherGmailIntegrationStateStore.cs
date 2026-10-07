using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Revestik.Api.Configuration;

namespace Revestik.Api.Services.GmailIntegration;

internal sealed class BankVoucherGmailIntegrationStateStore
    : IBankVoucherGmailIntegrationStateStore
{
    private const string ProtectorPurpose =
        "Revestik.BankVoucherGmailIntegration.State.v1";

    private readonly ProtectedGmailIntegrationStateStore inner;

    public BankVoucherGmailIntegrationStateStore(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<BankVoucherGmailIntegrationOptions> options,
        IWebHostEnvironment environment)
    {
        inner = new ProtectedGmailIntegrationStateStore(
            dataProtectionProvider,
            environment,
            options.Value.StateStoragePath,
            ProtectorPurpose);
    }

    public Task<GmailIntegrationState> LoadAsync(CancellationToken cancellationToken) =>
        inner.LoadAsync(cancellationToken);

    public Task SaveAsync(
        GmailIntegrationState state,
        CancellationToken cancellationToken) =>
        inner.SaveAsync(state, cancellationToken);

    public Task ClearAsync(CancellationToken cancellationToken) =>
        inner.ClearAsync(cancellationToken);
}
