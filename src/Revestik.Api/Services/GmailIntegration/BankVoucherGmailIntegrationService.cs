using Microsoft.Extensions.Options;
using Revestik.Api.Configuration;
using Revestik.Api.Services.BankVouchers;
using Revestik.Shared.Integrations.Gmail;

namespace Revestik.Api.Services.GmailIntegration;

internal sealed class BankVoucherGmailIntegrationService
    : IBankVoucherGmailIntegrationService
{
    private readonly GmailOAuthClient oauthClient;
    private readonly IBankVoucherGmailIntegrationStateStore stateStore;
    private readonly IBankVoucherGmailOAuthStateService oauthStateService;
    private readonly BankVoucherGmailSyncCoordinator coordinator;
    private readonly IBankVoucherIngestionService ingestionService;
    private readonly BankVoucherGmailIntegrationOptions options;
    private readonly ILogger<BankVoucherGmailIntegrationService> logger;

    public BankVoucherGmailIntegrationService(
        GmailOAuthClient oauthClient,
        IBankVoucherGmailIntegrationStateStore stateStore,
        IBankVoucherGmailOAuthStateService oauthStateService,
        BankVoucherGmailSyncCoordinator coordinator,
        IBankVoucherIngestionService ingestionService,
        IOptions<BankVoucherGmailIntegrationOptions> options,
        ILogger<BankVoucherGmailIntegrationService> logger)
    {
        this.oauthClient = oauthClient;
        this.stateStore = stateStore;
        this.oauthStateService = oauthStateService;
        this.coordinator = coordinator;
        this.ingestionService = ingestionService;
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
        {
            throw new GmailIntegrationException(
                "Google no devolvió un código de autorización.");
        }

        oauthStateService.Validate(state, userId);

        var token = await oauthClient.ExchangeAuthorizationCodeAsync(
            code,
            options.ClientId!,
            options.ClientSecret!,
            options.RedirectUri,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new GmailIntegrationException(
                "Google no devolvió un access token válido.");
        }

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

    public async Task<BankVoucherGmailSyncResultResponse> SyncAsync(
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        if (!coordinator.TryEnter(out var lease) || lease is null)
            throw new GmailSyncAlreadyRunningException();

        using (lease)
        {
            var state = await stateStore.LoadAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(state.RefreshToken) ||
                !string.Equals(
                    state.ConnectedMailbox,
                    options.ExpectedMailbox,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new GmailIntegrationException(
                    "Gmail no está conectado con la cuenta de vouchers configurada.");
            }

            try
            {
                var accessToken = await oauthClient.RefreshAccessTokenAsync(
                    state.RefreshToken,
                    options.ClientId!,
                    options.ClientSecret!,
                    cancellationToken);

                var result = await SynchronizeMailboxAsync(
                    accessToken,
                    state,
                    cancellationToken);

                if (result.Failed == 0)
                {
                    state.LastSuccessfulSyncUtc = result.CompletedAtUtc;
                    state.LastSyncError = null;
                }
                else
                {
                    state.LastSyncError =
                        $"La sincronización terminó con {result.Failed} mensaje(s) fallido(s). " +
                        "El cursor no avanzó para permitir reintento desde el último punto exitoso.";
                }

                await stateStore.SaveAsync(state, cancellationToken);

                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                state.LastSyncError = SanitizeSyncError(exception);
                await stateStore.SaveAsync(state, CancellationToken.None);
                throw;
            }
        }
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

    private async Task<BankVoucherGmailSyncResultResponse> SynchronizeMailboxAsync(
        string accessToken,
        GmailIntegrationState state,
        CancellationToken cancellationToken)
    {
        var fromUtc = state.LastSuccessfulSyncUtc.HasValue
            ? state.LastSuccessfulSyncUtc.Value.AddMinutes(-10)
            : DateTime.UtcNow.AddDays(-Math.Max(1, options.InitialLookbackDays));

        var afterUnix = new DateTimeOffset(fromUtc).ToUnixTimeSeconds();

        var sender = options.ExpectedSender.Trim();
        var expectedSubject = options.ExpectedSubject
            .Trim()
            .Replace("\"", "\\\"");

        var query =
            $"from:{sender} subject:\"{expectedSubject}\" after:{afterUnix}";

        var messageIds = await oauthClient.ListMessageIdsAsync(
            accessToken,
            query,
            options.MaxMessagesPerSync,
            cancellationToken);

        var messagesScanned = 0;
        var imported = 0;
        var duplicates = 0;
        var unrecognized = 0;
        var failed = 0;

        foreach (var messageId in messageIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            messagesScanned++;

            try
            {
                if (await ingestionService.ExistsAsync(
                        messageId,
                        cancellationToken))
                {
                    duplicates++;
                    continue;
                }

                var message = await oauthClient.GetMessageAsync(
                    accessToken,
                    messageId,
                    cancellationToken);

                var subject = GmailMimePartWalker.GetHeader(
                    message.Payload,
                    "Subject");

                var bodies = await ReadTextBodiesAsync(
                    accessToken,
                    messageId,
                    message.Payload,
                    cancellationToken);

                var outcome = await ingestionService.ImportAsync(
                    messageId,
                    subject,
                    bodies,
                    cancellationToken);

                switch (outcome)
                {
                    case BankVoucherImportOutcome.Imported:
                        imported++;
                        break;

                    case BankVoucherImportOutcome.Duplicate:
                        duplicates++;
                        break;

                    default:
                        unrecognized++;
                        break;
                }
            }
            catch (GmailRateLimitException)
            {
                logger.LogWarning(
                    "Bank voucher Gmail synchronization stopped after {MessagesScanned} message(s) because Gmail rate limiting was reached.",
                    messagesScanned);

                throw;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed++;

                logger.LogWarning(
                    exception,
                    "Unable to process bank voucher Gmail message {MessageId}.",
                    messageId);
            }
        }

        return new BankVoucherGmailSyncResultResponse(
            messagesScanned,
            imported,
            duplicates,
            unrecognized,
            failed,
            DateTime.UtcNow);
    }

    private async Task<IReadOnlyList<string>> ReadTextBodiesAsync(
        string accessToken,
        string messageId,
        GmailMessagePartDto? payload,
        CancellationToken cancellationToken)
    {
        var results = new List<string>();

        foreach (var body in GmailMimePartWalker.FindTextBodies(payload))
        {
            string? encoded = body.InlineData;

            if (string.IsNullOrWhiteSpace(encoded) &&
                !string.IsNullOrWhiteSpace(body.AttachmentId))
            {
                encoded = await oauthClient.GetMessageAttachmentDataAsync(
                    accessToken,
                    messageId,
                    body.AttachmentId,
                    cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(encoded))
                continue;

            try
            {
                results.Add(
                    GmailMimePartWalker.DecodeBase64UrlUtf8(encoded));
            }
            catch (FormatException exception)
            {
                logger.LogWarning(
                    exception,
                    "Gmail message {MessageId} contains an invalid base64url text body {BodyIdentity}.",
                    messageId,
                    body.Identity);
            }
        }

        return results;
    }

    private void EnsureConfigured()
    {
        if (!options.IsConfigured)
        {
            throw new GmailIntegrationException(
                "La integración Gmail de vouchers todavía no tiene ClientId y ClientSecret configurados.");
        }
    }

    private static string SanitizeSyncError(Exception exception)
    {
        var message = exception is GmailIntegrationException
            ? exception.Message
            : "La sincronización de Gmail de vouchers falló de forma inesperada.";

        return message.Length <= 250
            ? message
            : message[..250];
    }
}