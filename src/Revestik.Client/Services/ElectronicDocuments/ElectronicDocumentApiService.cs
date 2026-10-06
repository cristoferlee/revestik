using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Client.Services.ElectronicDocuments;

public sealed class ElectronicDocumentApiService(HttpClient httpClient)
    : IElectronicDocumentApiService
{
    public async Task<ElectronicDocumentListResponse> GetPageAsync(
        ElectronicDocumentListRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            BuildListUrl(request),
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadRequiredAsync<ElectronicDocumentListResponse>(
            response,
            "The API returned an empty received documents response.",
            cancellationToken);
    }

    public async Task<ElectronicDocumentSummaryResponse> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            "api/purchases/received-documents/summary",
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadRequiredAsync<ElectronicDocumentSummaryResponse>(
            response,
            "The API returned an empty received documents summary.",
            cancellationToken);
    }

    public async Task<ElectronicDocumentDetailResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"api/purchases/received-documents/{id}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<ElectronicDocumentDetailResponse>(
            cancellationToken);
    }

    public async Task<IReadOnlyList<ElectronicDocumentCategoryResponse>> GetCategoriesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"api/purchases/received-documents/categories?includeInactive={includeInactive.ToString().ToLowerInvariant()}",
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadRequiredAsync<List<ElectronicDocumentCategoryResponse>>(
            response,
            "The API returned an empty categories response.",
            cancellationToken);
    }

    public async Task<ElectronicDocumentCategoryResponse> CreateCategoryAsync(
        ElectronicDocumentCategoryUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/purchases/received-documents/categories",
            request,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadRequiredAsync<ElectronicDocumentCategoryResponse>(
            response,
            "The API returned an empty category response.",
            cancellationToken);
    }

    public async Task<ElectronicDocumentCategoryResponse?> UpdateCategoryAsync(
        int id,
        ElectronicDocumentCategoryUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"api/purchases/received-documents/categories/{id}",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<ElectronicDocumentCategoryResponse>(
            cancellationToken);
    }

    public async Task<IReadOnlyList<ElectronicDocumentLearningRuleResponse>> GetLearningRulesAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            "api/purchases/received-documents/learning-rules",
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadRequiredAsync<List<ElectronicDocumentLearningRuleResponse>>(
            response,
            "The API returned an empty learning rules response.",
            cancellationToken);
    }

    public async Task<ElectronicDocumentLearningRuleResponse?> UpdateLearningRuleAsync(
        ElectronicDocumentLearningRuleUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            "api/purchases/received-documents/learning-rules",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<ElectronicDocumentLearningRuleResponse>(
            cancellationToken);
    }

    public async Task<bool> DeleteLearningRuleAsync(
        string ruleType,
        int ruleId,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync(
            $"api/purchases/received-documents/learning-rules/{Uri.EscapeDataString(ruleType)}/{ruleId}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return true;
    }

    public async Task<ElectronicDocumentDetailResponse?> ClassifyAsync(
        int id,
        ElectronicDocumentClassifyRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"api/purchases/received-documents/{id}/classify",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<ElectronicDocumentDetailResponse>(
            cancellationToken);
    }

    public async Task<ReceivedDocumentInboxItemResponse> ImportAsync(
        Stream xmlStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(xmlStream);
        fileContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/xml");

        content.Add(fileContent, "file", fileName);

        using var response = await httpClient.PostAsync(
            "api/purchases/received-documents/import",
            content,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadRequiredAsync<ReceivedDocumentInboxItemResponse>(
            response,
            "The API returned an empty secure inbox response.",
            cancellationToken);
    }


    public async Task<IReadOnlyList<ReceivedDocumentInboxItemResponse>> GetInboxAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            "api/purchases/received-documents/inbox",
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var result = await ReadRequiredAsync<ReceivedDocumentInboxListResponse>(
            response,
            "The API returned an empty secure inbox response.",
            cancellationToken);

        return result.Items;
    }

    public async Task<ReceivedDocumentInboxAcceptResponse> AcceptInboxAsync(
        Guid inboxId,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync(
            $"api/purchases/received-documents/inbox/{inboxId}/accept",
            content: null,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadRequiredAsync<ReceivedDocumentInboxAcceptResponse>(
            response,
            "The API returned an empty inbox accept response.",
            cancellationToken);
    }

    public async Task<bool> RejectInboxAsync(
        Guid inboxId,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync(
            $"api/purchases/received-documents/inbox/{inboxId}/reject",
            content: null,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        await EnsureSuccessAsync(response, cancellationToken);
        return true;
    }

    private static string BuildListUrl(ElectronicDocumentListRequest request)
    {
        var parameters = new List<string>();

        AddOptional(parameters, "Search", request.Search);

        if (request.DocumentType.HasValue)
            AddOptional(parameters, "DocumentType", request.DocumentType.Value.ToString());

        if (request.ProcessingStatus.HasValue)
            AddOptional(parameters, "ProcessingStatus", request.ProcessingStatus.Value.ToString());

        if (request.CategoryId.HasValue)
            AddOptional(parameters, "CategoryId", request.CategoryId.Value.ToString(CultureInfo.InvariantCulture));

        if (request.DateFrom.HasValue)
            AddOptional(parameters, "DateFrom", request.DateFrom.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        if (request.DateTo.HasValue)
            AddOptional(parameters, "DateTo", request.DateTo.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        AddOptional(parameters, "Page", request.Page.ToString(CultureInfo.InvariantCulture));
        AddOptional(parameters, "PageSize", request.PageSize.ToString(CultureInfo.InvariantCulture));

        return $"api/purchases/received-documents?{string.Join("&", parameters)}";
    }

    private static void AddOptional(
        ICollection<string> parameters,
        string name,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add(
                $"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}");
        }
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
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = "La operación no pudo completarse.";

        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                using var json = JsonDocument.Parse(content);
                if (json.RootElement.TryGetProperty("error", out var error))
                {
                    message = error.GetString() ?? message;
                }
                else if (json.RootElement.TryGetProperty("detail", out var detail))
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
