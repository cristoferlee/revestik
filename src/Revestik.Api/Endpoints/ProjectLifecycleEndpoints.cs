using System.Security.Claims;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Projects;
using Revestik.Shared.Projects;
namespace Revestik.Api.Endpoints;
public static class ProjectLifecycleEndpoints
{
    public static IEndpointRouteBuilder MapProjectLifecycleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var g = endpoints.MapGroup("/api/projects/{projectId:int}/lifecycle")
            .WithTags("Project lifecycle").RequireAuthorization(PolicyNames.ManageProjects);
        g.MapGet("/history", async (int projectId, ProjectLifecycleService service, CancellationToken ct) =>
        {
            var result = await service.HistoryAsync(projectId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization(PolicyNames.AdministratorOnly);
        g.MapPut("/status", async (int projectId, ProjectTransitionRequest request, ClaimsPrincipal user,
            ProjectLifecycleService service, CancellationToken ct) =>
            await ExecuteAsync(user, (actor, admin) => service.TransitionAsync(projectId, request, actor, admin, ct)))
            .AddEndpointFilter<AntiforgeryValidationFilter>();
        g.MapPost("/close", async (int projectId, ProjectCloseRequest request, ClaimsPrincipal user,
            ProjectLifecycleService service, CancellationToken ct) =>
            await ExecuteAsync(user, (actor, admin) => service.CloseAsync(projectId, request, actor, admin, ct)))
            .AddEndpointFilter<AntiforgeryValidationFilter>();
        g.MapPost("/reopen", async (int projectId, ProjectReopenRequest request, ClaimsPrincipal user,
            ProjectLifecycleService service, CancellationToken ct) =>
            await ExecuteAsync(user, (actor, admin) => service.ReopenAsync(projectId, request, actor, admin, ct)))
            .AddEndpointFilter<AntiforgeryValidationFilter>();
        return endpoints;
    }
    private static async Task<IResult> ExecuteAsync(ClaimsPrincipal user,
        Func<string, bool, Task<ProjectLifecycleStatusResponse?>> action)
    {
        var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return Results.Unauthorized();
        try
        {
            var result = await action(actor, user.IsInRole(RoleNames.Administrator));
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (ProjectOperationException ex)
        {
            return Results.Problem(title: ex.Message, statusCode: ex.Reason switch
            {
                ProjectFailureReason.Forbidden => StatusCodes.Status403Forbidden,
                ProjectFailureReason.ConcurrencyConflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            });
        }
    }
}
