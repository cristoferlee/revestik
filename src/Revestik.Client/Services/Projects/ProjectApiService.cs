using System.Net;
using System.Net.Http.Json;
using Revestik.Shared.Projects;
using Revestik.Shared.Common;

namespace Revestik.Client.Services.Projects;

public sealed class ProjectApiService(HttpClient http)
{
    public async Task<PaginatedResponse<ProjectCustomerDirectoryItem>> CustomersWithProjectsAsync(
        int page, int pageSize = 20, string? search = null, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<PaginatedResponse<ProjectCustomerDirectoryItem>>(
            $"api/projects/customers-with-projects?page={page}&pageSize={pageSize}" +
            (string.IsNullOrWhiteSpace(search) ? "" : $"&search={Uri.EscapeDataString(search.Trim())}"), ct)
        ?? new PaginatedResponse<ProjectCustomerDirectoryItem>([], page, pageSize, 0);

    public async Task<IReadOnlyList<ProjectSummaryResponse>> ListAsync(int customerId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ProjectSummaryResponse>>($"api/projects?customerId={customerId}", ct) ?? [];

    public async Task<ProjectResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/projects/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<ProjectResponse>(cancellationToken: ct);
    }

    public Task<ProjectResponse> CreateAsync(int customerId, ProjectUpsertRequest request, CancellationToken ct = default) =>
        SendAsync<ProjectResponse>(HttpMethod.Post, $"api/projects?customerId={customerId}", request, ct);

    public Task<ProjectResponse> UpdateAsync(int id, ProjectUpsertRequest request, CancellationToken ct = default) =>
        SendAsync<ProjectResponse>(HttpMethod.Put, $"api/projects/{id}", request, ct);

    public Task<ProjectResponse> ReassignAsync(int id, ProjectReassignRequest request, CancellationToken ct = default) =>
        SendAsync<ProjectResponse>(HttpMethod.Put, $"api/projects/{id}/responsible", request, ct);

    public async Task DeleteAsync(int id, long version, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"api/projects/{id}?version={version}", ct);
        await EnsureAsync(response, ct);
    }

    public Task<ProjectLifecycleStatusResponse> TransitionAsync(int id, ProjectTransitionRequest request, CancellationToken ct = default) =>
        SendAsync<ProjectLifecycleStatusResponse>(HttpMethod.Put, $"api/projects/{id}/lifecycle/status", request, ct);

    public Task<ProjectLifecycleStatusResponse> CloseAsync(int id, ProjectCloseRequest request, CancellationToken ct = default) =>
        SendAsync<ProjectLifecycleStatusResponse>(HttpMethod.Post, $"api/projects/{id}/lifecycle/close", request, ct);

    public Task<ProjectLifecycleStatusResponse> ReopenAsync(int id, ProjectReopenRequest request, CancellationToken ct = default) =>
        SendAsync<ProjectLifecycleStatusResponse>(HttpMethod.Post, $"api/projects/{id}/lifecycle/reopen", request, ct);

    public async Task<ProjectBalanceResponse?> BalanceAsync(int id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/projects/{id}/payments/summary", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<ProjectBalanceResponse>(cancellationToken: ct);
    }

    public Task<ProjectBalanceResponse> ChangeAgreedAmountAsync(int id, ProjectAgreedAmountChangeRequest request, CancellationToken ct = default) =>
        SendAsync<ProjectBalanceResponse>(HttpMethod.Put, $"api/projects/{id}/agreed-amount", request, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        using var response = await http.SendAsync(request, ct);
        await EnsureAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
            ?? throw new InvalidOperationException("El servidor respondió sin información.");
    }

    private static async Task EnsureAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var code = response.StatusCode;
        string? title = null;
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken: ct);
            if (problem.TryGetProperty("title", out var value)) title = value.GetString();
        }
        catch (System.Text.Json.JsonException) { }
        catch (NotSupportedException) { }
        throw new ProjectClientException(code, title ?? (code switch
        {
            HttpStatusCode.BadRequest => "Revise los datos ingresados.",
            HttpStatusCode.Unauthorized => "Su sesión expiró. Inicie sesión nuevamente.",
            HttpStatusCode.Forbidden => "No tiene permiso para modificar este proyecto.",
            HttpStatusCode.NotFound => "El proyecto ya no está disponible.",
            HttpStatusCode.Conflict => "Los datos cambiaron. Actualice el proyecto antes de continuar.",
            _ => "No se pudo completar la operación."
        }));
    }
}

public sealed class ProjectClientException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
