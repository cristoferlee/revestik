using System.Security.Claims;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Projects;
using Revestik.Shared.Projects;

namespace Revestik.Api.Endpoints;

public static class ProjectActivityEndpoints
{
    public static IEndpointRouteBuilder MapProjectActivityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var categories = endpoints.MapGroup("/api/project-event-categories")
            .WithTags("Project event categories").RequireAuthorization(PolicyNames.ManageProjects);
        categories.MapGet("/", async (ProjectActivityService service, CancellationToken ct) =>
            Results.Ok(await service.CategoriesAsync(ct)));
        categories.MapPost("/", async (ProjectEventCategoryCreateRequest request, ProjectActivityService service, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.CreateCategoryAsync(request.Name, ct)); }
            catch (ProjectOperationException ex) { return ToProblem(ex); }
        }).RequireAuthorization(PolicyNames.AdministratorOnly).AddEndpointFilter<AntiforgeryValidationFilter>();
        categories.MapDelete("/{id:int}", async (int id, ProjectActivityService service, CancellationToken ct) =>
            await service.DeactivateCategoryAsync(id, ct) ? Results.NoContent() : Results.NotFound())
            .RequireAuthorization(PolicyNames.AdministratorOnly).AddEndpointFilter<AntiforgeryValidationFilter>();

        var events = endpoints.MapGroup("/api/projects/{projectId:int}/events")
            .WithTags("Project events").RequireAuthorization(PolicyNames.ManageProjects);
        events.MapGet("/", async (int projectId, int? categoryId, DateOnly? dateFrom, DateOnly? dateTo,
            string? createdByUserId, bool? includeVoided, ProjectActivityService service, CancellationToken ct) =>
        {
            try
            {
                var items = await service.ListAsync(projectId,
                    new(categoryId, dateFrom, dateTo, createdByUserId, includeVoided ?? false), ct);
                return items is null ? Results.NotFound() : Results.Ok(items);
            }
            catch (ProjectOperationException ex) { return ToProblem(ex); }
        });
        events.MapGet("/{eventId:int}", async (int projectId, int eventId, ProjectActivityService service, CancellationToken ct) =>
        {
            var result = await service.GetAsync(projectId, eventId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        events.MapPost("/", async (int projectId, ProjectEventUpsertRequest request,
            ClaimsPrincipal user, ProjectActivityService service, CancellationToken ct) =>
        {
            var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actor)) return Results.Unauthorized();
            try
            {
                var result = await service.CreateAsync(projectId, request, actor, user.IsInRole(RoleNames.Administrator), ct);
                return Results.Created($"/api/projects/{projectId}/events/{result.Id}", result);
            }
            catch (ProjectOperationException ex) { return ToProblem(ex); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>();
        events.MapPut("/{eventId:int}", async (int projectId, int eventId, ProjectEventUpsertRequest request,
            ClaimsPrincipal user, ProjectActivityService service, CancellationToken ct) =>
        {
            var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actor)) return Results.Unauthorized();
            try
            {
                var result = await service.UpdateAsync(projectId, eventId, request, actor,
                    user.IsInRole(RoleNames.Administrator), ct);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ProjectOperationException ex) { return ToProblem(ex); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>();
        events.MapPost("/{eventId:int}/void", async (int projectId, int eventId, ProjectEventVoidRequest request,
            ClaimsPrincipal user, ProjectActivityService service, CancellationToken ct) =>
        {
            var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actor)) return Results.Unauthorized();
            try
            {
                var result = await service.VoidAsync(projectId, eventId, request, actor,
                    user.IsInRole(RoleNames.Administrator), ct);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ProjectOperationException ex) { return ToProblem(ex); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>();
        return endpoints;
    }

    private static IResult ToProblem(ProjectOperationException exception) => Results.Problem(
        title: exception.Message, statusCode: exception.Reason switch
        {
            ProjectFailureReason.Forbidden => StatusCodes.Status403Forbidden,
            ProjectFailureReason.ConcurrencyConflict or ProjectFailureReason.Duplicate => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        });
}
