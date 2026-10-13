using System.Security.Claims;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Projects;
using Revestik.Shared.Projects;

namespace Revestik.Api.Endpoints;

public static class ProjectEventDetailEndpoints
{
    public static IEndpointRouteBuilder MapProjectEventDetailEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/projects/{projectId:int}/events/{eventId:int}")
            .WithTags("Project event details").RequireAuthorization(PolicyNames.ManageProjects);

        group.MapGet("/comments", async (int projectId, int eventId, ProjectEventDetailService service, CancellationToken ct) =>
        {
            var comments = await service.CommentsAsync(projectId, eventId, ct);
            return comments is null ? Results.NotFound() : Results.Ok(comments);
        });
        group.MapPost("/comments", async (int projectId, int eventId, ProjectCommentCreateRequest request,
            ClaimsPrincipal user, ProjectEventDetailService service, CancellationToken ct) =>
        {
            var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actor)) return Results.Unauthorized();
            try
            {
                var result = await service.AddCommentAsync(projectId, eventId, request, actor,
                    user.IsInRole(RoleNames.Administrator), ct);
                return result is null ? Results.NotFound() : Results.Created(
                    $"/api/projects/{projectId}/events/{eventId}/comments", result);
            }
            catch (ProjectOperationException ex) { return Problem(ex); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>();

        group.MapGet("/agreement", async (int projectId, int eventId, ProjectEventDetailService service, CancellationToken ct) =>
        {
            var agreement = await service.GetAgreementAsync(projectId, eventId, ct);
            return agreement is null ? Results.NotFound() : Results.Ok(agreement);
        });
        group.MapPut("/agreement", async (int projectId, int eventId, ProjectAgreementUpsertRequest request,
            ClaimsPrincipal user, ProjectEventDetailService service, CancellationToken ct) =>
        {
            var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actor)) return Results.Unauthorized();
            try
            {
                var result = await service.SaveAgreementAsync(projectId, eventId, request, actor,
                    user.IsInRole(RoleNames.Administrator), ct);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ProjectOperationException ex) { return Problem(ex); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>();

        group.MapGet("/history", async (int projectId, int eventId, ProjectEventDetailService service, CancellationToken ct) =>
        {
            var history = await service.EventHistoryAsync(projectId, eventId, ct);
            return history is null ? Results.NotFound() : Results.Ok(history);
        }).RequireAuthorization(PolicyNames.AdministratorOnly);
        group.MapGet("/agreement/history", async (int projectId, int eventId, ProjectEventDetailService service, CancellationToken ct) =>
        {
            var history = await service.AgreementHistoryAsync(projectId, eventId, ct);
            return history is null ? Results.NotFound() : Results.Ok(history);
        }).RequireAuthorization(PolicyNames.AdministratorOnly);
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
