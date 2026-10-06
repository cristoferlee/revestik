using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Revestik.Shared.Cabys;
using Revestik.Shared.Common;

namespace Revestik.Client.Services.Cabys;

public sealed class CabysApiService(HttpClient httpClient) : ICabysApiService
{
    public async Task<CabysCatalogStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            "api/cabys/status",
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredAsync<CabysCatalogStatusResponse>(
            response,
            "La API devolvió un estado CAByS vacío.",
            cancellationToken);
    }

    public async Task<CabysCatalogLoadResponse> LoadBundledCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync(
            "api/cabys/catalog/load-bundled",
            null,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredAsync<CabysCatalogLoadResponse>(
            response,
            "La API devolvió una respuesta vacía al cargar CAByS.",
            cancellationToken);
    }

    public async Task<PaginatedResponse<CabysListItemResponse>> SearchAsync(
        CabysSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            parameters.Add(
                $"Search={Uri.EscapeDataString(request.Search.Trim())}");
        }

        parameters.Add($"Page={request.Page.ToString(CultureInfo.InvariantCulture)}");
        parameters.Add($"PageSize={request.PageSize.ToString(CultureInfo.InvariantCulture)}");

        using var response = await httpClient.GetAsync(
            $"api/cabys?{string.Join("&", parameters)}",
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredAsync<PaginatedResponse<CabysListItemResponse>>(
            response,
            "La API devolvió una página CAByS vacía.",
            cancellationToken);
    }

    public async Task<CabysDetailResponse?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"api/cabys/{Uri.EscapeDataString(code)}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CabysDetailResponse>(
            cancellationToken);
    }

    private static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        string message,
        CancellationToken cancellationToken)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        return value ?? throw new InvalidOperationException(message);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = "La operación CAByS no pudo completarse.";

        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                using var json = JsonDocument.Parse(content);
                if (json.RootElement.TryGetProperty("detail", out var detail))
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
