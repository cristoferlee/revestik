using System.Net;
using System.Net.Http.Json;
using Revestik.Shared.Projects;

namespace Revestik.Client.Services.Projects;

public sealed class ProjectWorkspaceApiService(HttpClient http)
{
    private static string Root(int id) => $"api/projects/{id}";
    public Uri FileUrl(int projectId, int documentId, int? versionId = null, bool preview = false) =>
        new(http.BaseAddress!, $"{Root(projectId)}/documents/{documentId}/" +
            (versionId is int v ? $"versions/{v}/content" : preview ? "preview" : "content"));

    public Task<IReadOnlyList<ProjectEventCategoryResponse>> CategoriesAsync(CancellationToken ct = default) =>
        GetListAsync<ProjectEventCategoryResponse>("api/project-event-categories", ct);
    public Task<ProjectEventCategoryResponse> CreateCategoryAsync(string name, CancellationToken ct = default) =>
        SendAsync<ProjectEventCategoryResponse>(HttpMethod.Post, "api/project-event-categories", new { Name = name }, ct);
    public Task DeactivateCategoryAsync(int id, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"api/project-event-categories/{id}", ct);
    public Task<IReadOnlyList<ProjectEventResponse>> EventsAsync(int id, int? category = null, bool voided = false, CancellationToken ct = default) =>
        GetListAsync<ProjectEventResponse>($"{Root(id)}/events?includeVoided={voided.ToString().ToLowerInvariant()}" + (category is int c ? $"&categoryId={c}" : ""), ct);
    public Task<ProjectEventResponse> CreateEventAsync(int id, ProjectEventUpsertRequest body, CancellationToken ct = default) =>
        SendAsync<ProjectEventResponse>(HttpMethod.Post, $"{Root(id)}/events", body, ct);
    public Task<ProjectEventResponse> UpdateEventAsync(int id, int eventId, ProjectEventUpsertRequest body, CancellationToken ct = default) =>
        SendAsync<ProjectEventResponse>(HttpMethod.Put, $"{Root(id)}/events/{eventId}", body, ct);
    public Task<ProjectEventResponse> VoidEventAsync(int id, int eventId, ProjectEventVoidRequest body, CancellationToken ct = default) =>
        SendAsync<ProjectEventResponse>(HttpMethod.Post, $"{Root(id)}/events/{eventId}/void", body, ct);
    public Task<IReadOnlyList<ProjectCommentResponse>> CommentsAsync(int id, int eventId, CancellationToken ct = default) =>
        GetListAsync<ProjectCommentResponse>($"{Root(id)}/events/{eventId}/comments", ct);
    public Task<ProjectCommentResponse> CommentAsync(int id, int eventId, ProjectCommentCreateRequest body, CancellationToken ct = default) =>
        SendAsync<ProjectCommentResponse>(HttpMethod.Post, $"{Root(id)}/events/{eventId}/comments", body, ct);
    public Task<ProjectAgreementResponse?> AgreementAsync(int id, int eventId, CancellationToken ct = default) =>
        GetOptionalAsync<ProjectAgreementResponse>($"{Root(id)}/events/{eventId}/agreement", ct);
    public Task<ProjectAgreementResponse> SaveAgreementAsync(int id, int eventId, ProjectAgreementUpsertRequest body, CancellationToken ct = default) =>
        SendAsync<ProjectAgreementResponse>(HttpMethod.Put, $"{Root(id)}/events/{eventId}/agreement", body, ct);
    public Task<ProjectEventDeadlineResponse?> DeadlineAsync(int id, int eventId, CancellationToken ct = default) =>
        GetOptionalAsync<ProjectEventDeadlineResponse>($"{Root(id)}/events/{eventId}/deadline", ct);
    public Task<ProjectEventDeadlineResponse> SetDeadlineAsync(int id, int eventId, ProjectEventDeadlineSetRequest body, CancellationToken ct = default) =>
        SendAsync<ProjectEventDeadlineResponse>(HttpMethod.Put, $"{Root(id)}/events/{eventId}/deadline", body, ct);
    public Task<IReadOnlyList<ProjectTaskResponse>> TasksAsync(int id, CancellationToken ct = default) =>
        GetListAsync<ProjectTaskResponse>($"{Root(id)}/tasks", ct);
    public Task<ProjectTaskResponse> CreateTaskAsync(int id, ProjectTaskCreateRequest body, CancellationToken ct = default) =>
        SendAsync<ProjectTaskResponse>(HttpMethod.Post, $"{Root(id)}/tasks", body, ct);
    public Task<ProjectTaskResponse> UpdateTaskAsync(int id, int taskId, ProjectTaskUpdateRequest body, CancellationToken ct = default) =>
        SendAsync<ProjectTaskResponse>(HttpMethod.Put, $"{Root(id)}/tasks/{taskId}", body, ct);
    public Task<IReadOnlyList<ProjectDocumentLinkResponse>> DocumentLinksAsync(int id, CancellationToken ct = default) =>
        GetListAsync<ProjectDocumentLinkResponse>($"{Root(id)}/document-links", ct);
    public Task<IReadOnlyList<ProjectDocumentListItem>> DocumentsAsync(int id, CancellationToken ct = default) =>
        GetListAsync<ProjectDocumentListItem>($"{Root(id)}/documents", ct);
    public Task<ProjectDocumentDetails?> DocumentAsync(int id, int documentId, CancellationToken ct = default) =>
        GetOptionalAsync<ProjectDocumentDetails>($"{Root(id)}/documents/{documentId}", ct);
    public async Task<ProjectDocumentListItem> UploadAsync(int id, string name, string category, Stream stream, string fileName, string contentType, int? documentId = null, long? version = null, CancellationToken ct = default)
    {
        using var data = new MultipartFormDataContent();
        using var file = new StreamContent(stream);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        data.Add(file, "file", fileName);
        if (documentId is null) { data.Add(new StringContent(name), "displayName"); data.Add(new StringContent(category), "category"); }
        else data.Add(new StringContent((version ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)), "expectedDocumentVersion");
        var path = $"{Root(id)}/documents" + (documentId is int d ? $"/{d}/versions" : "");
        using var response = await http.PostAsync(path, data, ct);
        await EnsureAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<ProjectDocumentListItem>(cancellationToken: ct)
            ?? throw new InvalidOperationException("La respuesta de documentos está vacía.");
    }
    public Task LinkEventAsync(int id, int documentId, int eventId, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Post, $"{Root(id)}/documents/{documentId}/events/{eventId}", ct);
    public Task UnlinkEventAsync(int id, int documentId, int eventId, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"{Root(id)}/documents/{documentId}/events/{eventId}", ct);
    public Task DeleteDocumentAsync(int id, int documentId, long version, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"{Root(id)}/documents/{documentId}?expectedDocumentVersion={version}", ct);

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct);
        await EnsureAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<List<T>>(cancellationToken: ct) ?? [];
    }
    private async Task<T?> GetOptionalAsync<T>(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return default;
        await EnsureAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }
    private async Task<T> SendAsync<T>(HttpMethod method, string path, object body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        using var response = await http.SendAsync(request, ct);
        await EnsureAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
            ?? throw new InvalidOperationException("La respuesta está vacía.");
    }
    private async Task SendNoContentAsync(HttpMethod method, string path, CancellationToken ct)
    {
        using var response = await http.SendAsync(new HttpRequestMessage(method, path), ct);
        await EnsureAsync(response, ct);
    }
    private static async Task EnsureAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        string? title = null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.TryGetProperty("title", out var t)) title = t.GetString();
        }
        catch (System.Text.Json.JsonException) { }
        throw new ProjectClientException(response.StatusCode, title ?? (response.StatusCode switch
        {
            HttpStatusCode.Forbidden => "No tiene permisos para realizar esta operación.",
            HttpStatusCode.Conflict => "Los datos cambiaron; vuelva a cargar antes de guardar.",
            HttpStatusCode.Unauthorized => "Su sesión expiró.",
            HttpStatusCode.NotFound => "El registro ya no está disponible.",
            _ => "No fue posible completar la operación."
        }));
    }
}
