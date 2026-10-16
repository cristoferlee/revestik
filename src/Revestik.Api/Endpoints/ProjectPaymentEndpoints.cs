using System.Security.Claims;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Projects;
using Revestik.Shared.Projects;

namespace Revestik.Api.Endpoints;

public static class ProjectPaymentEndpoints
{
    public static IEndpointRouteBuilder MapProjectPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects/{projectId:int}")
            .WithTags("Project payments").RequireAuthorization(PolicyNames.ManageProjects);
        group.MapGet("/payments", async (int projectId, ProjectPaymentService service, CancellationToken ct) =>
        {
            var result = await service.ListAsync(projectId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapGet("/payments/summary", async (int projectId, ProjectPaymentService service, CancellationToken ct) =>
        {
            var result = await service.SummaryAsync(projectId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapGet("/payments/{paymentId:int}", async (int projectId, int paymentId, ProjectPaymentService service, CancellationToken ct) =>
        {
            var result = await service.GetAsync(projectId, paymentId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapGet("/payments/{paymentId:int}/history", async (int projectId, int paymentId, ProjectPaymentService service, CancellationToken ct) =>
        {
            var result = await service.HistoryAsync(projectId, paymentId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapGet("/agreed-amount/history", async (int projectId, ProjectPaymentService service, CancellationToken ct) =>
        {
            var result = await service.AmountHistoryAsync(projectId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapPost("/payments", async (int projectId, ProjectPaymentUpsertRequest body, ClaimsPrincipal user, ProjectPaymentService service, CancellationToken ct) =>
            await Execute(user, async (actor, admin) =>
            {
                var result = await service.CreateAsync(projectId, body, actor, admin, ct);
                return Results.Created($"/api/projects/{projectId}/payments/{result.Id}", result);
            })).AddEndpointFilter<AntiforgeryValidationFilter>();
        group.MapPut("/payments/{paymentId:int}", async (int projectId, int paymentId, ProjectPaymentUpsertRequest body, ClaimsPrincipal user, ProjectPaymentService service, CancellationToken ct) =>
            await Execute(user, async (actor, admin) =>
            {
                var result = await service.CorrectAsync(projectId, paymentId, body, actor, admin, ct);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })).AddEndpointFilter<AntiforgeryValidationFilter>();
        group.MapPost("/payments/{paymentId:int}/void", async (int projectId, int paymentId, ProjectPaymentVoidRequest body, ClaimsPrincipal user, ProjectPaymentService service, CancellationToken ct) =>
            await Execute(user, async (actor, admin) =>
                await service.VoidAsync(projectId, paymentId, body, actor, admin, ct) ? Results.NoContent() : Results.NotFound()))
            .AddEndpointFilter<AntiforgeryValidationFilter>();
        group.MapPut("/agreed-amount", async (int projectId, ProjectAgreedAmountChangeRequest body, ClaimsPrincipal user, ProjectPaymentService service, CancellationToken ct) =>
            await Execute(user, async (actor, admin) => Results.Ok(await service.ChangeAgreedAmountAsync(projectId, body, actor, admin, ct))))
            .AddEndpointFilter<AntiforgeryValidationFilter>();
        group.MapPost("/payments/{paymentId:int}/documents/{documentId:int}", async (int projectId, int paymentId, int documentId, ClaimsPrincipal user, ProjectPaymentService service, CancellationToken ct) =>
            await Execute(user, async (actor, admin) =>
                await service.LinkDocumentAsync(projectId, paymentId, documentId, actor, admin, ct) ? Results.NoContent() : Results.NotFound()))
            .AddEndpointFilter<AntiforgeryValidationFilter>();
        group.MapDelete("/payments/{paymentId:int}/documents/{documentId:int}", async (int projectId, int paymentId, int documentId, ClaimsPrincipal user, ProjectPaymentService service, CancellationToken ct) =>
            await Execute(user, async (actor, admin) =>
                await service.UnlinkDocumentAsync(projectId, paymentId, documentId, actor, admin, ct) ? Results.NoContent() : Results.NotFound()))
            .AddEndpointFilter<AntiforgeryValidationFilter>();
        return app;
    }

    private static async Task<IResult> Execute(ClaimsPrincipal user, Func<string, bool, Task<IResult>> action)
    {
        var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return Results.Unauthorized();
        try { return await action(actor, user.IsInRole(RoleNames.Administrator)); }
        catch (ProjectOperationException e)
        {
            return Results.Problem(title: e.Message, statusCode: e.Reason switch
            {
                ProjectFailureReason.Forbidden => 403,
                ProjectFailureReason.ConcurrencyConflict or ProjectFailureReason.Duplicate => 409,
                _ => 400
            });
        }
    }
}
