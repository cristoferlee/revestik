using Revestik.Api.Extensions;
using System.Security.Claims;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Projects;
using Revestik.Api.Services.Projects.Documents;

namespace Revestik.Api.Endpoints;

// B04 D: operations live on the same authenticated ManageProjects route group as B04 C.
public static class ProjectDocumentLifecycleEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{documentId:int}/versions", async (int projectId, int documentId,
            HttpRequest request, ClaimsPrincipal user, ProjectDocumentService service,
            CancellationToken ct) =>
        {
            var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actor)) return Results.Unauthorized();
            if (!request.HasFormContentType) return Results.BadRequest("Se requiere multipart/form-data.");
            if (request.ContentLength > 21L * 1024 * 1024) return Results.StatusCode(413);
            try
            {
                var form = await request.ReadFormAsync(ct);
                if (form.Files.Count != 1 || form.Files[0].Length == 0 ||
                    form.Files[0].Length > ProjectDocumentUploadPolicy.MaxUploadBytes)
                    return Results.BadRequest("Adjunte un único archivo válido de hasta 20 MB.");
                if (!long.TryParse(form["expectedDocumentVersion"].ToString(), out var expected) || expected < 1)
                    return Results.BadRequest("expectedDocumentVersion inválida.");
                var file = form.Files[0];
                await using var original = file.OpenReadStream();
                var validated = await ProjectDocumentUploadPolicy.ValidateAsync(original, file.FileName, ct);
                await using var content = new MemoryStream(validated.Bytes, writable: false);
                var result = await service.AddVersionAsync(projectId, documentId, expected,
                    file.FileName, validated.ContentType, content, actor,
                    user.IsInRole(RoleNames.Administrator), ct);
                return Results.Ok(result);
            }
            catch (ProjectOperationException e) { return Problem(e); }
            catch (InvalidDataException e) { return Results.BadRequest(e.Message); }
            catch (BadHttpRequestException) { return Results.BadRequest("Carga multipart inválida."); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>()
          .RequireRateLimiting(RevestikRateLimiting.DocumentUpload);

        group.MapPost("/{documentId:int}/events/{eventId:int}", async (int projectId,
            int documentId, int eventId, ClaimsPrincipal user, ProjectDocumentService service,
            CancellationToken ct) =>
        {
            var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actor)) return Results.Unauthorized();
            try
            {
                await service.LinkEventAsync(projectId, documentId, eventId,
                    actor, user.IsInRole(RoleNames.Administrator), ct);
                return Results.NoContent();
            }
            catch (ProjectOperationException e) { return Problem(e); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>();

        group.MapDelete("/{documentId:int}/events/{eventId:int}", async (int projectId,
            int documentId, int eventId, ClaimsPrincipal user, ProjectDocumentService service,
            CancellationToken ct) =>
        {
            var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actor)) return Results.Unauthorized();
            try
            {
                await service.UnlinkEventAsync(projectId, documentId, eventId,
                    actor, user.IsInRole(RoleNames.Administrator), ct);
                return Results.NoContent();
            }
            catch (ProjectOperationException e) { return Problem(e); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>();

        group.MapDelete("/{documentId:int}", async (int projectId, int documentId,
            long? expectedDocumentVersion, ClaimsPrincipal user, ProjectDocumentService service,
            CancellationToken ct) =>
        {
            var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actor)) return Results.Unauthorized();
            if (expectedDocumentVersion is null or < 1)
                return Results.BadRequest("expectedDocumentVersion es requerida.");
            try
            {
                await service.SoftDeleteAsync(projectId, documentId, expectedDocumentVersion.Value,
                    actor, user.IsInRole(RoleNames.Administrator), ct);
                return Results.NoContent();
            }
            catch (ProjectOperationException e) { return Problem(e); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>();
    }

    private static IResult Problem(ProjectOperationException e) => Results.Problem(
        title: e.Message, statusCode: e.Reason switch
        {
            ProjectFailureReason.Forbidden => StatusCodes.Status403Forbidden,
            ProjectFailureReason.ConcurrencyConflict or ProjectFailureReason.Duplicate => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        });
}
