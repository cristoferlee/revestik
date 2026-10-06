using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Revestik.Api.Configuration;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.GmailIntegration;

internal sealed class GmailIntegrationService : IGmailIntegrationService
{
    private const int MaxXmlBytes = 5 * 1024 * 1024;
    private const int CurrentRejectedValidationRevision = 2;
    private const string GmailReadonlyScope =
        "https://www.googleapis.com/auth/gmail.readonly";
    private const string AuthorizationEndpoint =
        "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint =
        "https://oauth2.googleapis.com/token";
    private const string RevokeEndpoint =
        "https://oauth2.googleapis.com/revoke";
    private const string GmailApiBaseUrl =
        "https://gmail.googleapis.com/gmail/v1/";

    private readonly IHttpClientFactory httpClientFactory;
    private readonly IReceivedDocumentInboxService inboxService;
    private readonly IGmailIntegrationStateStore stateStore;
    private readonly IGmailOAuthStateService oauthStateService;
    private readonly GmailSyncCoordinator coordinator;
    private readonly GmailIntegrationOptions options;
    private readonly ILogger<GmailIntegrationService> logger;

    public GmailIntegrationService(
        IHttpClientFactory httpClientFactory,
        IReceivedDocumentInboxService inboxService,
        IGmailIntegrationStateStore stateStore,
        IGmailOAuthStateService oauthStateService,
        GmailSyncCoordinator coordinator,
        IOptions<GmailIntegrationOptions> options,
        ILogger<GmailIntegrationService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.inboxService = inboxService;
        this.stateStore = stateStore;
        this.oauthStateService = oauthStateService;
        this.coordinator = coordinator;
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task<GmailIntegrationStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        var state = await stateStore.LoadAsync(cancellationToken);
        var connected = options.IsConfigured &&
            !string.IsNullOrWhiteSpace(state.RefreshToken) &&
            string.Equals(
                state.ConnectedMailbox,
                options.ExpectedMailbox,
                StringComparison.OrdinalIgnoreCase);

        return new GmailIntegrationStatusResponse(
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

        var state = oauthStateService.Create(userId);

        var parameters = new Dictionary<string, string?>
        {
            ["client_id"] = options.ClientId,
            ["redirect_uri"] = options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = GmailReadonlyScope,
            ["access_type"] = "offline",
            ["include_granted_scopes"] = "true",
            ["prompt"] = "consent",
            ["state"] = state
        };

        return AddQueryString(AuthorizationEndpoint, parameters);
    }

    public async Task CompleteAuthorizationAsync(
        string code,
        string state,
        string userId,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(code))
            throw new GmailIntegrationException("Google no devolviÃ³ un cÃ³digo de autorizaciÃ³n.");

        oauthStateService.Validate(state, userId);

        var token = await ExchangeAuthorizationCodeAsync(code, cancellationToken);
        if (string.IsNullOrWhiteSpace(token.AccessToken))
            throw new GmailIntegrationException("Google no devolviÃ³ un access token vÃ¡lido.");

        if (string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            throw new GmailIntegrationException(
                "Google no devolviÃ³ un refresh token. Desconecta el acceso previo en Google e intenta conectar nuevamente.");
        }

        var profile = await GetProfileAsync(token.AccessToken, cancellationToken);
        if (!string.Equals(
                profile.EmailAddress,
                options.ExpectedMailbox,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new GmailIntegrationException(
                $"La cuenta autorizada ({profile.EmailAddress}) no corresponde al buzÃ³n configurado.");
        }

        var existing = await stateStore.LoadAsync(cancellationToken);
        existing.ConnectedMailbox = profile.EmailAddress;
        existing.RefreshToken = token.RefreshToken;
        existing.ConnectedAtUtc = DateTime.UtcNow;
        existing.LastSyncError = null;

        await stateStore.SaveAsync(existing, cancellationToken);
    }

    public async Task<GmailSyncResultResponse> SyncAsync(
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
                    "Gmail no estÃ¡ conectado con la cuenta de facturaciÃ³n configurada.");
            }

            try
            {
                var accessToken = await RefreshAccessTokenAsync(
                    state.RefreshToken,
                    cancellationToken);

                var result = await SynchronizeMailboxAsync(
                    accessToken,
                    state,
                    cancellationToken);

                state.LastSuccessfulSyncUtc = result.CompletedAtUtc;
                state.LastSyncError = null;
                PruneProcessedAttachments(state);
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
                using var client = httpClientFactory.CreateClient();
                using var content = new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["token"] = state.RefreshToken
                    });

                using var response = await client.PostAsync(
                    RevokeEndpoint,
                    content,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning(
                        "Google token revocation returned HTTP {StatusCode}. Local credentials will still be removed.",
                        (int)response.StatusCode);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Google token revocation failed. Local credentials will still be removed.");
            }
        }

        await stateStore.ClearAsync(cancellationToken);
    }

    private async Task<GmailSyncResultResponse> SynchronizeMailboxAsync(
        string accessToken,
        GmailIntegrationState state,
        CancellationToken cancellationToken)
    {
        RemoveRetryableRejectedAttachments(state);

        var processed = state.ProcessedAttachments
            .Select(x => x.Key)
            .ToHashSet(StringComparer.Ordinal);

        var fromUtc = state.LastSuccessfulSyncUtc.HasValue
            ? state.LastSuccessfulSyncUtc.Value.AddMinutes(-10)
            : DateTime.UtcNow.AddDays(-Math.Max(1, options.InitialLookbackDays));

        var afterUnix = new DateTimeOffset(fromUtc).ToUnixTimeSeconds();
        var query = $"has:attachment filename:xml after:{afterUnix}";

        var messageIds = await ListMessageIdsAsync(
            accessToken,
            query,
            cancellationToken);

        var messagesScanned = 0;
        var xmlAttachmentsFound = 0;
        var staged = 0;
        var duplicates = 0;
        var recognizedNotEnabled = 0;
        var rejected = 0;
        var failed = 0;
        var alreadyProcessed = 0;

        foreach (var messageId in messageIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            messagesScanned++;

            GmailMessageDto message;
            try
            {
                message = await GetMessageAsync(
                    accessToken,
                    messageId,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed++;
                logger.LogWarning(
                    exception,
                    "Unable to read Gmail message {MessageId}.",
                    messageId);
                continue;
            }

            foreach (var attachment in GmailMimePartWalker.FindXmlAttachments(message.Payload))
            {
                xmlAttachmentsFound++;
                var ledgerKey = $"{messageId}:{attachment.Identity}";

                if (processed.Contains(ledgerKey))
                {
                    alreadyProcessed++;
                    continue;
                }

                if (attachment.DeclaredSize > MaxXmlBytes)
                {
                    rejected++;
                    MarkProcessed(state, processed, ledgerKey, "Rejected:TooLarge");
                    await stateStore.SaveAsync(state, cancellationToken);
                    continue;
                }

                try
                {
                    var bytes = await GetAttachmentBytesAsync(
                        accessToken,
                        messageId,
                        attachment,
                        cancellationToken);

                    var inboxItem = await inboxService.StageAsync(
                        bytes,
                        attachment.FileName,
                        $"Gmail:{messageId}",
                        cancellationToken);

                    switch (inboxItem.Status)
                    {
                        case ReceivedDocumentInboxStatus.Duplicate:
                            duplicates++;
                            break;
                        case ReceivedDocumentInboxStatus.RecognizedNotEnabled:
                            recognizedNotEnabled++;
                            break;
                        default:
                            staged++;
                            break;
                    }

                    MarkProcessed(
                        state,
                        processed,
                        ledgerKey,
                        $"Staged:{inboxItem.Status}");

                    await stateStore.SaveAsync(state, cancellationToken);
                }
                catch (ReceivedDocumentInboxException exception)
                {
                    rejected++;
                    MarkProcessed(
                        state,
                        processed,
                        ledgerKey,
                        "Rejected:" + Truncate(exception.Message, 120));

                    await stateStore.SaveAsync(state, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failed++;
                    logger.LogWarning(
                        exception,
                        "Unable to process Gmail XML attachment {AttachmentKey}.",
                        ledgerKey);
                }
            }
        }

        return new GmailSyncResultResponse(
            messagesScanned,
            xmlAttachmentsFound,
            staged,
            duplicates,
            recognizedNotEnabled,
            rejected,
            failed,
            alreadyProcessed,
            DateTime.UtcNow);
    }

    private async Task<IReadOnlyList<string>> ListMessageIdsAsync(
        string accessToken,
        string query,
        CancellationToken cancellationToken)
    {
        var results = new List<string>();
        string? pageToken = null;
        var maximum = Math.Clamp(options.MaxMessagesPerSync, 1, 500);

        do
        {
            var remaining = maximum - results.Count;
            if (remaining <= 0)
                break;

            var url = AddQueryString(
                $"{GmailApiBaseUrl}users/me/messages",
                new Dictionary<string, string?>
                {
                    ["q"] = query,
                    ["maxResults"] = Math.Min(remaining, 100)
                        .ToString(CultureInfo.InvariantCulture),
                    ["pageToken"] = pageToken,
                    ["includeSpamTrash"] = "false"
                });

            var page = await GetAuthorizedJsonAsync<GmailMessageListDto>(
                accessToken,
                url,
                cancellationToken);

            results.AddRange(
                page.Messages
                    .Select(x => x.Id)
                    .Where(x => !string.IsNullOrWhiteSpace(x)));

            pageToken = page.NextPageToken;
        }
        while (!string.IsNullOrWhiteSpace(pageToken) && results.Count < maximum);

        return results;
    }

    private Task<GmailMessageDto> GetMessageAsync(
        string accessToken,
        string messageId,
        CancellationToken cancellationToken) =>
        GetAuthorizedJsonAsync<GmailMessageDto>(
            accessToken,
            AddQueryString(
                $"{GmailApiBaseUrl}users/me/messages/{Uri.EscapeDataString(messageId)}",
                new Dictionary<string, string?> { ["format"] = "full" }),
            cancellationToken);

    private async Task<byte[]> GetAttachmentBytesAsync(
        string accessToken,
        string messageId,
        GmailXmlAttachmentCandidate attachment,
        CancellationToken cancellationToken)
    {
        string data;

        if (!string.IsNullOrWhiteSpace(attachment.InlineData))
        {
            data = attachment.InlineData;
        }
        else if (!string.IsNullOrWhiteSpace(attachment.AttachmentId))
        {
            var dto = await GetAuthorizedJsonAsync<GmailAttachmentDto>(
                accessToken,
                $"{GmailApiBaseUrl}users/me/messages/{Uri.EscapeDataString(messageId)}/attachments/{Uri.EscapeDataString(attachment.AttachmentId)}",
                cancellationToken);

            data = dto.Data;
        }
        else
        {
            throw new GmailIntegrationException(
                "El adjunto XML de Gmail no contiene datos descargables.");
        }

        try
        {
            return GmailMimePartWalker.DecodeBase64Url(data);
        }
        catch (FormatException)
        {
            throw new GmailIntegrationException(
                "Gmail devolviÃ³ un adjunto XML con codificaciÃ³n base64url invÃ¡lida.");
        }
    }

    private async Task<GoogleTokenResponse> ExchangeAuthorizationCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient();
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = options.ClientId!,
                ["client_secret"] = options.ClientSecret!,
                ["redirect_uri"] = options.RedirectUri,
                ["grant_type"] = "authorization_code"
            });

        using var response = await client.PostAsync(
            TokenEndpoint,
            content,
            cancellationToken);

        var token = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(
            cancellationToken);

        if (!response.IsSuccessStatusCode || token is null)
        {
            throw new GmailIntegrationException(
                token?.ErrorDescription ??
                "Google rechazÃ³ el intercambio del cÃ³digo OAuth.");
        }

        return token;
    }

    private async Task<string> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient();
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["refresh_token"] = refreshToken,
                ["client_id"] = options.ClientId!,
                ["client_secret"] = options.ClientSecret!,
                ["grant_type"] = "refresh_token"
            });

        using var response = await client.PostAsync(
            TokenEndpoint,
            content,
            cancellationToken);

        var token = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(
            cancellationToken);

        if (!response.IsSuccessStatusCode ||
            token is null ||
            string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new GmailIntegrationException(
                token?.ErrorDescription ??
                "No fue posible renovar el acceso de Gmail. Vuelve a conectar la cuenta.");
        }

        return token.AccessToken;
    }

    private async Task<GmailProfileDto> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken) =>
        await GetAuthorizedJsonAsync<GmailProfileDto>(
            accessToken,
            $"{GmailApiBaseUrl}users/me/profile",
            cancellationToken);

    private async Task<T> GetAuthorizedJsonAsync<T>(
        string accessToken,
        string url,
        CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new GmailIntegrationException(
                $"Gmail API devolviÃ³ HTTP {(int)response.StatusCode}. {Truncate(body, 180)}");
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new GmailIntegrationException(
                "Gmail API devolviÃ³ una respuesta vacÃ­a.");
    }

    private void EnsureConfigured()
    {
        if (!options.IsConfigured)
        {
            throw new GmailIntegrationException(
                "La integraciÃ³n Gmail todavÃ­a no tiene ClientId y ClientSecret configurados.");
        }
    }

    private static void MarkProcessed(
        GmailIntegrationState state,
        ISet<string> processed,
        string key,
        string outcome)
    {
        if (!processed.Add(key))
            return;

        state.ProcessedAttachments.Add(
            new GmailProcessedAttachment
            {
                Key = key,
                ProcessedAtUtc = DateTime.UtcNow,
                Outcome = outcome,
                ValidationRevision = outcome.StartsWith("Rejected:", StringComparison.Ordinal)
                    ? CurrentRejectedValidationRevision
                    : 0
            });
    }


    internal static void RemoveRetryableRejectedAttachments(GmailIntegrationState state)
    {
        state.ProcessedAttachments.RemoveAll(item =>
            item.Outcome.StartsWith("Rejected:", StringComparison.Ordinal) &&
            item.ValidationRevision < CurrentRejectedValidationRevision);
    }

    private static void PruneProcessedAttachments(GmailIntegrationState state)
    {
        var cutoff = DateTime.UtcNow.AddDays(-365);

        state.ProcessedAttachments = state.ProcessedAttachments
            .Where(x => x.ProcessedAtUtc >= cutoff)
            .OrderByDescending(x => x.ProcessedAtUtc)
            .Take(10_000)
            .ToList();
    }

    private static string SanitizeSyncError(Exception exception) =>
        Truncate(
            exception is GmailIntegrationException
                ? exception.Message
                : "La sincronizaciÃ³n de Gmail fallÃ³ de forma inesperada.",
            250);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength
            ? value
            : value[..maxLength];

    private static string AddQueryString(
        string baseUrl,
        IReadOnlyDictionary<string, string?> parameters)
    {
        var values = parameters
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x =>
                $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value!)}");

        return $"{baseUrl}?{string.Join("&", values)}";
    }
}

