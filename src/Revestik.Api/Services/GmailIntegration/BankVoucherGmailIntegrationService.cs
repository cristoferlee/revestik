using Microsoft.Extensions.Options;
using Revestik.Api.Configuration;
using Revestik.Shared.Integrations.Gmail;

namespace Revestik.Api.Services.GmailIntegration;

internal sealed class BankVoucherGmailIntegrationService
    : IBankVoucherGmailIntegrationService
{
    private readonly GmailOAuthClient oauthClient;
    private readonly IBankVoucherGmailIntegrationStateStore stateStore;
    private readonly IBankVoucherGmailOAuthStateService oauthStateService;
    private readonly BankVoucherGmailSyncCoordinator coordinator;
    private readonly BankVoucherGmailIntegrationOptions options;
    private readonly ILogger<BankVoucherGmailIntegrationService> logger;

    public BankVoucherGmailIntegrationService(
        GmailOAuthClient oauthClient,
        IBankVoucherGmailIntegrationStateStore stateStore,
        IBankVoucherGmailOAuthStateService oauthStateService,
        BankVoucherGmailSyncCoordinator coordinator,
        IOptions<BankVoucherGmailIntegrationOptions> options,
        ILogger<BankVoucherGmailIntegrationService> logger)
    {
        this.oauthClient = oauthClient;
        this.stateStore = stateStore;
        this.oauthStateService = oauthStateService;
        this.coordinator = coordinator;
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task<GmailMailboxStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        var state = await stateStore.LoadAsync(cancellationToken);
        var connected = options.IsConfigured &&
            !string.IsNullOrWhiteSpace(state.RefreshToken) &&
            string.Equals(
                state.ConnectedMailbox,
                options.ExpectedMailbox,
                StringComparison.OrdinalIgnoreCase);

        return new GmailMailboxStatusResponse(
            options.IsConfigured,
            connected,
            options.ExpectedMailbox,
            state.ConnectedMailbox,
            state.ConnectedAtUtc,
            state.LastSuccessfulSyncUtc,
            state.LastSyncError,
            coordinator.IsSyncing);
    }

    public string CreateAuthorizationUrl(string userId)
    {
        EnsureConfigured();

        var protectedState = oauthStateService.Create(userId);
        return oauthClient.CreateAuthorizationUrl(
            options.ClientId!,
            options.RedirectUri,
            protectedState);
    }

    public async Task CompleteAuthorizationAsync(
        string code,
        string state,
        string userId,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(code))
            throw new GmailIntegrationException(
                "Google no devolvió un código de autorización.");

        oauthStateService.Validate(state, userId);

        var token = await oauthClient.ExchangeAuthorizationCodeAsync(
            code,
            options.ClientId!,
            options.ClientSecret!,
            options.RedirectUri,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(token.AccessToken))
            throw new GmailIntegrationException(
                "Google no devolvió un access token válido.");

        if (string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            throw new GmailIntegrationException(
                "Google no devolvió un refresh token. Desconecta el acceso previo en Google e intenta conectar nuevamente.");
        }

        var connectedMailbox = await oauthClient.GetConnectedMailboxAsync(
            token.AccessToken,
            cancellationToken);

        if (!string.Equals(
                connectedMailbox,
                options.ExpectedMailbox,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new GmailIntegrationException(
                $"La cuenta autorizada ({connectedMailbox}) no corresponde al buzón configurado.");
        }

        var existing = await stateStore.LoadAsync(cancellationToken);
        existing.ConnectedMailbox = connectedMailbox;
        existing.RefreshToken = token.RefreshToken;
        existing.ConnectedAtUtc = DateTime.UtcNow;
        existing.LastSyncError = null;

        await stateStore.SaveAsync(existing, cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        if (coordinator.IsSyncing)
            throw new GmailSyncAlreadyRunningException();

        var state = await stateStore.LoadAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(state.RefreshToken))
        {
            try
            {
                var revoked = await oauthClient.TryRevokeAsync(
                    state.RefreshToken,
                    cancellationToken);

                if (!revoked)
                {
                    logger.LogWarning(
                        "Google token revocation failed for bank voucher Gmail. Local credentials will still be removed.");
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Google token revocation failed for bank voucher Gmail. Local credentials will still be removed.");
            }
        }

        await stateStore.ClearAsync(cancellationToken);
    }

    private void EnsureConfigured()
    {
        if (!options.IsConfigured)
        {
            throw new GmailIntegrationException(
                "La integración Gmail de vouchers todavía no tiene ClientId y ClientSecret configurados.");
        }
    }
}
