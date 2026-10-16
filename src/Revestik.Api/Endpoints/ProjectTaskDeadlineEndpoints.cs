using System.Security.Claims;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Projects;
using Revestik.Shared.Projects;

namespace Revestik.Api.Endpoints;

public static class ProjectTaskDeadlineEndpoints
{
    public static IEndpointRouteBuilder MapProjectTaskDeadlineEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var tasks = endpoints.MapGroup("/api/projects/{projectId:int}/tasks")
            .WithTags("Project tasks").RequireAuthorization(PolicyNames.ManageProjects);
        tasks.MapGet("/", async (int projectId, ProjectTaskDeadlineService service, CancellationToken ct) =>
        {
            var result = await service.TasksAsync(projectId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        tasks.MapGet("/{taskId:int}", async (int projectId, int taskId, ProjectTaskDeadlineService service, CancellationToken ct) =>
        {
            var result = await service.TaskAsync(projectId, taskId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        tasks.MapGet("/{taskId:int}/history", async (int projectId, int taskId,
            ProjectTaskDeadlineService service, CancellationToken ct) =>
        {
            var result = await service.TaskHistoryAsync(projectId, taskId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization(PolicyNames.AdministratorOnly);
        tasks.MapPost("/", async (int projectId, ProjectTaskCreateRequest request, ClaimsPrincipal user,
            ProjectTaskDeadlineService service, CancellationToken ct) =>
        {
            var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(actor)) return Results.Unauthorized();
            try
            {
                var result = await service.CreateTaskAsync(projectId, request, actor,
                    user.IsInRole(RoleNames.Administrator), ct);
                return Results.Created($"/api/projects/{projectId}/tasks/{result.Id}", result);
            }
            catch (ProjectOperationException ex) { return Problem(ex); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>();
        tasks.MapPut("/{taskId:int}", async (int projectId, int taskId, ProjectTaskUpdateRequest request,
            ClaimsPrincipal user, ProjectTaskDeadlineService service, CancellationToken ct) =>
        {
            var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(actor)) return Results.Unauthorized();
            try
            {
                var result = await service.UpdateTaskAsync(projectId, taskId, request, actor,
                    user.IsInRole(RoleNames.Administrator), ct);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ProjectOperationException ex) { return Problem(ex); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>();

        var deadlines = endpoints.MapGroup("/api/projects/{projectId:int}/events/{eventId:int}/deadline")
            .WithTags("Project event deadlines").RequireAuthorization(PolicyNames.ManageProjects);
        deadlines.MapGet("/", async (int projectId, int eventId,
            ProjectTaskDeadlineService service, CancellationToken ct) =>
        {
            var result = await service.GetDeadlineAsync(projectId, eventId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        deadlines.MapGet("/history", async (int projectId, int eventId,
            ProjectTaskDeadlineService service, CancellationToken ct) =>
        {
            var result = await service.DeadlineHistoryAsync(projectId, eventId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization(PolicyNames.AdministratorOnly);
        deadlines.MapPut("/", async (int projectId, int eventId, ProjectEventDeadlineSetRequest request,
            ClaimsPrincipal user, ProjectTaskDeadlineService service, CancellationToken ct) =>
        {
            var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(actor)) return Results.Unauthorized();
            try
            {
                var result = await service.SetDeadlineAsync(projectId, eventId, request, actor,
                    user.IsInRole(RoleNames.Administrator), ct);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ProjectOperationException ex) { return Problem(ex); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>();
        return endpoints;
    }

    private static IResult Problem(ProjectOperationException ex) => Results.Problem(title: ex.Message,
        statusCode: ex.Reason switch
        {
            ProjectFailureReason.Forbidden => StatusCodes.Status403Forbidden,
            ProjectFailureReason.ConcurrencyConflict or ProjectFailureReason.Duplicate => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        });
}
