using System.Net;
using System.Net.Http.Json;
using Revestik.Shared.Projects;

namespace Revestik.Client.Services.Projects;

// Milestone response types are mirrored here to avoid an Api-project dependency in WebAssembly.
public sealed record ProjectMilestoneItem(int Id, string Name, string Description, decimal Weight,
    int SortOrder, bool IsCompleted, bool IsExcluded, long Version);
public sealed record ProjectProgressView(int ProjectId, bool IsConfigured, bool IsActive,
    decimal? Percent, long? PlanVersion, IReadOnlyList<ProjectMilestoneItem> Milestones);

public sealed class ProjectFinancePlanApiService(HttpClient http)
{
    private static string Path(int id) => $"api/projects/{id}";
    public Task<List<ProjectPaymentResponse>> PaymentsAsync(int id) => ReadAsync<List<ProjectPaymentResponse>>($"{Path(id)}/payments");
    public Task<ProjectBalanceResponse> BalanceAsync(int id) => ReadAsync<ProjectBalanceResponse>($"{Path(id)}/payments/summary");
    public Task<ProjectPaymentResponse> AddPaymentAsync(int id, ProjectPaymentUpsertRequest body) => SendAsync<ProjectPaymentResponse>(HttpMethod.Post, $"{Path(id)}/payments", body);
    public Task<ProjectPaymentResponse> UpdatePaymentAsync(int id, int paymentId, ProjectPaymentUpsertRequest body) => SendAsync<ProjectPaymentResponse>(HttpMethod.Put, $"{Path(id)}/payments/{paymentId}", body);
    public Task VoidPaymentAsync(int id, int paymentId, ProjectPaymentVoidRequest body) => SendAsync(HttpMethod.Post, $"{Path(id)}/payments/{paymentId}/void", body);
    public Task<List<ProjectPaymentRevisionResponse>> PaymentHistoryAsync(int id, int paymentId) => ReadAsync<List<ProjectPaymentRevisionResponse>>($"{Path(id)}/payments/{paymentId}/history");
    public Task<List<ProjectAgreedAmountRevisionResponse>> AmountHistoryAsync(int id) => ReadAsync<List<ProjectAgreedAmountRevisionResponse>>($"{Path(id)}/agreed-amount/history");
    public Task LinkPaymentDocumentAsync(int id, int paymentId, int documentId) => SendAsync(HttpMethod.Post, $"{Path(id)}/payments/{paymentId}/documents/{documentId}", null);
    public Task UnlinkPaymentDocumentAsync(int id, int paymentId, int documentId) => SendAsync(HttpMethod.Delete, $"{Path(id)}/payments/{paymentId}/documents/{documentId}", null);
    public Task<ProjectProgressView> ProgressAsync(int id) => ReadAsync<ProjectProgressView>($"{Path(id)}/milestones");
    public Task<ProjectProgressView> ConfigureAsync(int id, ConfigureProjectMilestonesRequest body) => SendAsync<ProjectProgressView>(HttpMethod.Put, $"{Path(id)}/milestones", body);
    public Task<ProjectProgressView> ActivateAsync(int id, long version) => SendAsync<ProjectProgressView>(HttpMethod.Post, $"{Path(id)}/milestones/activate", new ProjectMilestonePlanVersionRequest(version));
    public Task<ProjectProgressView> CompleteAsync(int id, int milestoneId, SetProjectMilestoneCompletionRequest body) => SendAsync<ProjectProgressView>(HttpMethod.Put, $"{Path(id)}/milestones/{milestoneId}/completion", body);
    public Task<List<ProjectMilestoneTemplateResponse>> TemplatesAsync() => ReadAsync<List<ProjectMilestoneTemplateResponse>>("api/project-milestone-templates");
    public Task<ProjectProgressView> ApplyTemplateAsync(int id, ApplyProjectMilestoneTemplateRequest body) => SendAsync<ProjectProgressView>(HttpMethod.Post, $"{Path(id)}/milestones/apply-template", body);
    public Task<ProjectMilestoneTemplateResponse> CreateTemplateAsync(CreateProjectMilestoneTemplateRequest body) => SendAsync<ProjectMilestoneTemplateResponse>(HttpMethod.Post, "api/project-milestone-templates", body);
    public Task DeleteTemplateAsync(int id) => SendAsync(HttpMethod.Delete, $"api/project-milestone-templates/{id}", null);

    private async Task<T> ReadAsync<T>(string path)
    {
        using var response = await http.GetAsync(path);
        await CheckAsync(response);
        return await response.Content.ReadFromJsonAsync<T>() ?? throw new InvalidOperationException("Respuesta vacía del servidor.");
    }
    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request);
        await CheckAsync(response);
        return await response.Content.ReadFromJsonAsync<T>() ?? throw new InvalidOperationException("Respuesta vacía del servidor.");
    }
    private async Task SendAsync(HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request);
        await CheckAsync(response);
    }
    private static async Task CheckAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        string? detail = null;
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (json.RootElement.TryGetProperty("title", out var title)) detail = title.GetString();
        }
        catch (System.Text.Json.JsonException) { }
        throw new ProjectClientException(response.StatusCode, detail ?? (response.StatusCode switch
        {
            HttpStatusCode.Forbidden => "No tiene permisos para modificar este proyecto.",
            HttpStatusCode.Conflict => "Los datos cambiaron. Actualice para continuar.",
            HttpStatusCode.Unauthorized => "La sesión expiró.",
            _ => "No fue posible completar la operación."
        }));
    }
}
