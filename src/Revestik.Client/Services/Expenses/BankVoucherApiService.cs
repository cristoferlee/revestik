using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Revestik.Shared.Expenses;
using Revestik.Shared.Integrations.Gmail;

namespace Revestik.Client.Services.Expenses;

public sealed class BankVoucherApiService(HttpClient httpClient)
    : IBankVoucherApiService
{
    public async Task<GmailMailboxStatusResponse> GetGmailStatusAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            "api/integrations/gmail/bank-vouchers/status",
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadRequiredAsync<GmailMailboxStatusResponse>(
            response,
            "La API devolvió un estado vacío para Gmail de vouchers.",
            cancellationToken);
    }

    public async Task<string> GetAuthorizationUrlAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            "api/integrations/gmail/bank-vouchers/connect-url",
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var result = await ReadRequiredAsync<GmailMailboxAuthorizationUrlResponse>(
            response,
            "La API devolvió una URL OAuth vacía.",
            cancellationToken);

        return result.AuthorizationUrl;
    }

    public async Task<BankVoucherGmailSyncResultResponse> SyncAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync(
            "api/integrations/gmail/bank-vouchers/sync",
            content: null,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadRequiredAsync<BankVoucherGmailSyncResultResponse>(
            response,
            "La API devolvió un resultado de sincronización vacío.",
            cancellationToken);
    }

    public async Task DisconnectAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync(
            "api/integrations/gmail/bank-vouchers/disconnect",
            content: null,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<BankVoucherPageResponse> GetVouchersAsync(
        BankVoucherListRequest request,
        CancellationToken cancellationToken = default)
    {
        var parameters = new List<string>();
        AddDate(parameters, "DateFrom", request.DateFrom);
        AddDate(parameters, "DateTo", request.DateTo);

        if (request.Status.HasValue)
        {
            parameters.Add(
                "Status=" +
                ((int)request.Status.Value).ToString(CultureInfo.InvariantCulture));
        }

        if (request.ExcludeNeedsReview)
            parameters.Add("ExcludeNeedsReview=true");

        parameters.Add(
            $"Page={request.Page.ToString(CultureInfo.InvariantCulture)}");
        parameters.Add(
            $"PageSize={request.PageSize.ToString(CultureInfo.InvariantCulture)}");

        var url =
            $"api/bank-vouchers?{string.Join("&", parameters)}";

        using var response = await httpClient.GetAsync(
            url,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadRequiredAsync<BankVoucherPageResponse>(
            response,
            "La API devolvió una página vacía de vouchers.",
            cancellationToken);
    }

    public Task AcceptAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        PostAsync(
            $"api/bank-vouchers/{id}/accept",
            content: null,
            cancellationToken);

    public Task IgnoreAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        PostAsync(
            $"api/bank-vouchers/{id}/ignore",
            content: null,
            cancellationToken);

    public Task MatchAsync(
        int id,
        int electronicDocumentId,
        CancellationToken cancellationToken = default) =>
        PostAsync(
            $"api/bank-vouchers/{id}/match",
            JsonContent.Create(
                new BankVoucherMatchRequest
                {
                    ElectronicDocumentId = electronicDocumentId
                }),
            cancellationToken);

    private async Task PostAsync(
        string url,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsync(
            url,
            content,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static void AddDate(
        ICollection<string> parameters,
        string name,
        DateOnly? value)
    {
        if (!value.HasValue)
            return;

        parameters.Add(
            $"{name}=" +
            Uri.EscapeDataString(
                value.Value.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture)));
    }

    private static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        string message,
        CancellationToken cancellationToken)
    {
        var result = await response.Content
            .ReadFromJsonAsync<T>(cancellationToken);

        return result ?? throw new InvalidOperationException(message);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var content = await response.Content
            .ReadAsStringAsync(cancellationToken);

        var message = "La operación no pudo completarse.";

        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                using var json = JsonDocument.Parse(content);

                if (json.RootElement.TryGetProperty(
                        "detail",
                        out var detail))
                {
                    message = detail.GetString() ?? message;
                }
            }
            catch (JsonException)
            {
            }
        }

        throw new InvalidOperationException(message);
    }
}
