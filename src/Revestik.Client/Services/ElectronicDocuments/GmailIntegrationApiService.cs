using System.Net.Http.Json;
using System.Text.Json;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Client.Services.ElectronicDocuments;

public sealed class GmailIntegrationApiService(HttpClient httpClient)
    : IGmailIntegrationApiService
{
    public async Task<GmailIntegrationStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            "api/integrations/gmail/status",
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadRequiredAsync<GmailIntegrationStatusResponse>(
            response,
            "The API returned an empty Gmail integration status.",
            cancellationToken);
    }

    public async Task<string> GetAuthorizationUrlAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            "api/integrations/gmail/connect-url",
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var result = await ReadRequiredAsync<GmailAuthorizationUrlResponse>(
            response,
            "The API returned an empty Gmail authorization URL.",
            cancellationToken);

        return result.AuthorizationUrl;
    }

    public async Task<GmailSyncResultResponse> SyncAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync(
            "api/integrations/gmail/sync",
            content: null,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadRequiredAsync<GmailSyncResultResponse>(
            response,
            "The API returned an empty Gmail synchronization result.",
            cancellationToken);
    }

    public async Task DisconnectAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync(
            "api/integrations/gmail/disconnect",
            content: null,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        string message,
        CancellationToken cancellationToken)
    {
        var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        return result ?? throw new InvalidOperationException(message);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = "La operación no pudo completarse.";

        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                using var json = JsonDocument.Parse(content);
                if (json.RootElement.TryGetProperty("detail", out var detail))
                    message = detail.GetString() ?? message;
                else if (json.RootElement.TryGetProperty("error", out var error))
                    message = error.GetString() ?? message;
            }
            catch (JsonException)
            {
            }
        }

        throw new InvalidOperationException(message);
    }
}
