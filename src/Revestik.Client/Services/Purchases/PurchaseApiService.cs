using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Revestik.Shared.Common;
using Revestik.Shared.Purchases;

namespace Revestik.Client.Services.Purchases;

public sealed class PurchaseApiService(HttpClient httpClient)
    : IPurchaseApiService
{
    public async Task<PaginatedResponse<PurchaseListItemResponse>> GetPageAsync(
        PurchaseListRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            BuildListUrl(request),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await ReadRequiredAsync<
            PaginatedResponse<PurchaseListItemResponse>>(
            response,
            "The API returned an empty purchases page response.",
            cancellationToken);
    }

    public async Task<PurchaseApSummaryResponse> GetApSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            "api/purchases/ap-summary",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await ReadRequiredAsync<PurchaseApSummaryResponse>(
            response,
            "The API returned an empty AP summary response.",
            cancellationToken);
    }

    public async Task<PurchaseDueAlertResponse> GetDueAlertsAsync(
        PurchaseAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        var url =
            $"api/purchases/alerts?ShortWindowDays=" +
            $"{request.ShortWindowDays.ToString(CultureInfo.InvariantCulture)}" +
            $"&LongWindowDays=" +
            $"{request.LongWindowDays.ToString(CultureInfo.InvariantCulture)}";

        using var response = await httpClient.GetAsync(
            url,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await ReadRequiredAsync<PurchaseDueAlertResponse>(
            response,
            "The API returned an empty purchase alerts response.",
            cancellationToken);
    }

    public async Task<SupplierPurchaseHistoryResponse?> GetSupplierHistoryAsync(
        int supplierId,
        SupplierPurchaseHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var url =
            $"api/purchases/suppliers/{supplierId}/history" +
            $"?Page={request.Page.ToString(CultureInfo.InvariantCulture)}" +
            $"&PageSize={request.PageSize.ToString(CultureInfo.InvariantCulture)}";

        using var response = await httpClient.GetAsync(
            url,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<SupplierPurchaseHistoryResponse>(
                cancellationToken);
    }

    public async Task<PurchaseResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"api/purchases/{id}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PurchaseResponse>(
            cancellationToken);
    }

    public async Task<PurchaseResponse> CreateAsync(
        PurchaseCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/purchases",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await ReadRequiredAsync<PurchaseResponse>(
            response,
            "The API returned an empty purchase response.",
            cancellationToken);
    }

    public async Task<PurchaseResponse?> RegisterPaymentAsync(
        int id,
        PurchasePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"api/purchases/{id}/payments",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await ReadRequiredAsync<PurchaseResponse>(
            response,
            "The API returned an empty purchase payment response.",
            cancellationToken);
    }

    public async Task<PurchaseResponse?> VoidPaymentAsync(
        int id,
        int paymentId,
        VoidPurchasePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"api/purchases/{id}/payments/{paymentId}/void",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await ReadRequiredAsync<PurchaseResponse>(
            response,
            "The API returned an empty purchase payment void response.",
            cancellationToken);
    }

    private static string BuildListUrl(
        PurchaseListRequest request)
    {
        var parameters = new List<string>();

        if (request.SupplierId.HasValue)
        {
            Add(
                parameters,
                "SupplierId",
                request.SupplierId.Value.ToString(
                    CultureInfo.InvariantCulture));
        }

        if (request.DateFrom.HasValue)
        {
            Add(
                parameters,
                "DateFrom",
                request.DateFrom.Value.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture));
        }

        if (request.DateTo.HasValue)
        {
            Add(
                parameters,
                "DateTo",
                request.DateTo.Value.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture));
        }

        if (request.Currency.HasValue)
        {
            Add(
                parameters,
                "Currency",
                request.Currency.Value.ToString());
        }

        if (request.PaymentType.HasValue)
        {
            Add(
                parameters,
                "PaymentType",
                request.PaymentType.Value.ToString());
        }

        if (request.BalanceStatus.HasValue)
        {
            Add(
                parameters,
                "BalanceStatus",
                request.BalanceStatus.Value.ToString());
        }

        if (request.OverdueOnly.HasValue)
        {
            Add(
                parameters,
                "OverdueOnly",
                request.OverdueOnly.Value
                    .ToString()
                    .ToLowerInvariant());
        }

        Add(
            parameters,
            "Page",
            request.Page.ToString(
                CultureInfo.InvariantCulture));

        Add(
            parameters,
            "PageSize",
            request.PageSize.ToString(
                CultureInfo.InvariantCulture));

        return $"api/purchases?{string.Join("&", parameters)}";
    }

    private static void Add(
        ICollection<string> parameters,
        string name,
        string value)
    {
        parameters.Add(
            $"{Uri.EscapeDataString(name)}=" +
            $"{Uri.EscapeDataString(value)}");
    }

    private static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        string message,
        CancellationToken cancellationToken)
    {
        var result = await response.Content
            .ReadFromJsonAsync<T>(
                cancellationToken);

        return result ??
            throw new InvalidOperationException(message);
    }
}