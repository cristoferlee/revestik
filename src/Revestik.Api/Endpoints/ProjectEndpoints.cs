using System.Security.Claims;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Projects;
using Revestik.Shared.Projects;

namespace Revestik.Api.Endpoints;

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/projects")
            .WithTags("Projects")
            .RequireAuthorization(PolicyNames.ManageProjects);

        group.MapGet("/", async (int customerId, IProjectService service, CancellationToken ct) =>
            customerId <= 0 ? Results.BadRequest() : Results.Ok(await service.ListAsync(customerId, ct)))
            .WithName("ListProjects");

        group.MapGet("/{id:int}", async (int id, IProjectService service, CancellationToken ct) =>
        {
            var project = await service.GetAsync(id, ct);
            return project is null ? Results.NotFound() : Results.Ok(project);
        }).WithName("GetProject");

        group.MapPost("/", async (int customerId, ProjectUpsertRequest request,
            ClaimsPrincipal user, IProjectService service, CancellationToken ct) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            try
            {
                var project = await service.CreateAsync(customerId, request, userId, ct);
                return Results.Created($"/api/projects/{project.Id}", project);
            }
            catch (ProjectOperationException e) { return ToProblem(e); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>().WithName("CreateProject");

        group.MapPut("/{id:int}", async (int id, ProjectUpsertRequest request,
            ClaimsPrincipal user, IProjectService service, CancellationToken ct) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            try
            {
                var project = await service.UpdateAsync(id, request, userId,
                    user.IsInRole(RoleNames.Administrator), ct);
                return project is null ? Results.NotFound() : Results.Ok(project);
            }
            catch (ProjectOperationException e) { return ToProblem(e); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>().WithName("UpdateProject");

        group.MapPut("/{id:int}/responsible", async (int id, ProjectReassignRequest request,
            IProjectService service, CancellationToken ct) =>
        {
            try
            {
                var project = await service.ReassignAsync(id, request.ResponsibleUserId, request.Version, ct);
                return project is null ? Results.NotFound() : Results.Ok(project);
            }
            catch (ProjectOperationException e) { return ToProblem(e); }
        }).RequireAuthorization(PolicyNames.AdministratorOnly)
            .AddEndpointFilter<AntiforgeryValidationFilter>().WithName("ReassignProject");

        group.MapDelete("/{id:int}", async (int id, long? version, ClaimsPrincipal user,
            IProjectService service, CancellationToken ct) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            try
            {
                return await service.DeleteAsync(id, userId, user.IsInRole(RoleNames.Administrator), version ?? 0, ct)
                    ? Results.NoContent() : Results.NotFound();
            }
            catch (ProjectOperationException e) { return ToProblem(e); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>().WithName("DeleteProject");

        return endpoints;
    }

    private static IResult ToProblem(ProjectOperationException e) =>
        Results.Problem(title: e.Message, statusCode: e.Reason switch
        {
            ProjectFailureReason.Invalid => StatusCodes.Status400BadRequest,
            ProjectFailureReason.Duplicate => StatusCodes.Status409Conflict,
            ProjectFailureReason.ConcurrencyConflict => StatusCodes.Status409Conflict,
            ProjectFailureReason.InactiveCustomer => StatusCodes.Status409Conflict,
            ProjectFailureReason.Forbidden => StatusCodes.Status403Forbidden,
            ProjectFailureReason.UserNotFound => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        });
}
