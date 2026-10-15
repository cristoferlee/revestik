using System.Security.Claims;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Projects;
using Revestik.Shared.Projects;

namespace Revestik.Api.Endpoints;

public static class ProjectMilestoneEndpoints
{
    public static IEndpointRouteBuilder MapProjectMilestoneEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/projects/{projectId:int}/milestones")
            .WithTags("Project milestones").RequireAuthorization(PolicyNames.ManageProjects);

        group.MapGet("/", async (int projectId, ProjectMilestoneService service, CancellationToken ct) =>
        {
            var result = await service.GetAsync(projectId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPut("/", async (int projectId, ConfigureProjectMilestonesRequest request,
            ClaimsPrincipal user, ProjectMilestoneService service, CancellationToken ct) =>
        {
            var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actor)) return Results.Unauthorized();
            if (request?.Milestones is null) return Results.BadRequest();
            try
            {
                var inputs = request.Milestones.Select(x =>
                    new MilestoneInput(x.Id, x.Name, x.Description, x.Weight, x.SortOrder)).ToList();
                return Results.Ok(await service.ConfigureAsync(projectId, inputs, request.PlanVersion,
                    actor, user.IsInRole(RoleNames.Administrator), ct));
            }
            catch (ProjectOperationException ex) { return Problem(ex); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>();

        group.MapPost("/activate", async (int projectId, ProjectMilestonePlanVersionRequest request,
            ClaimsPrincipal user, ProjectMilestoneService service, CancellationToken ct) =>
        {
            var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actor)) return Results.Unauthorized();
            try
            {
                return Results.Ok(await service.ActivateAsync(projectId, request.PlanVersion,
                    actor, user.IsInRole(RoleNames.Administrator), ct));
            }
            catch (ProjectOperationException ex) { return Problem(ex); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>();

        group.MapPut("/{milestoneId:int}/completion", async (int projectId, int milestoneId,
            SetProjectMilestoneCompletionRequest request, ClaimsPrincipal user,
            ProjectMilestoneService service, CancellationToken ct) =>
        {
            var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actor)) return Results.Unauthorized();
            try
            {
                return Results.Ok(await service.SetCompletedAsync(projectId, milestoneId,
                    request.Completed, request.PlanVersion, request.MilestoneVersion, actor,
                    user.IsInRole(RoleNames.Administrator), request.Reason, ct));
            }
            catch (ProjectOperationException ex) { return Problem(ex); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>();

        group.MapPost("/apply-template", async (int projectId, ApplyProjectMilestoneTemplateRequest request,
            ClaimsPrincipal user, ProjectMilestoneTemplateService service, CancellationToken ct) =>
        {
            var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actor)) return Results.Unauthorized();
            try
            {
                return Results.Ok(await service.ApplyAsync(projectId, request.TemplateId,
                    request.PlanVersion, actor, user.IsInRole(RoleNames.Administrator), ct));
            }
            catch (ProjectOperationException ex) { return Problem(ex); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>();

        var templates = endpoints.MapGroup("/api/project-milestone-templates")
            .WithTags("Project milestone templates").RequireAuthorization(PolicyNames.ManageProjects);

        templates.MapGet("/", async (ProjectMilestoneTemplateService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)));

        templates.MapPost("/", async (CreateProjectMilestoneTemplateRequest request,
            ClaimsPrincipal user, ProjectMilestoneTemplateService service, CancellationToken ct) =>
        {
            var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actor)) return Results.Unauthorized();
            try { return Results.Ok(await service.CreateAsync(request, actor, ct)); }
            catch (ProjectOperationException ex) { return Problem(ex); }
        }).RequireAuthorization(PolicyNames.AdministratorOnly).AddEndpointFilter<AntiforgeryValidationFilter>();

        templates.MapDelete("/{templateId:int}", async (int templateId,
            ProjectMilestoneTemplateService service, CancellationToken ct) =>
            await service.DeactivateAsync(templateId, ct) ? Results.NoContent() : Results.NotFound())
            .RequireAuthorization(PolicyNames.AdministratorOnly).AddEndpointFilter<AntiforgeryValidationFilter>();
        return endpoints;
    }

    private static IResult Problem(ProjectOperationException ex) => Results.Problem(
        title: ex.Message, statusCode: ex.Reason switch
        {
            ProjectFailureReason.Forbidden => StatusCodes.Status403Forbidden,
            ProjectFailureReason.ConcurrencyConflict or ProjectFailureReason.Duplicate => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        });
}
