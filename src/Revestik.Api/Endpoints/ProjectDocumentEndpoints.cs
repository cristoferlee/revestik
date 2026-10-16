using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Projects;
using Revestik.Api.Services.Projects.Documents;
using Revestik.Shared.Projects;

namespace Revestik.Api.Endpoints;

public static class ProjectDocumentEndpoints
{
    public static IEndpointRouteBuilder MapProjectDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/projects/{projectId:int}/documents")
            .WithTags("Project documents").RequireAuthorization(PolicyNames.ManageProjects);

        group.MapGet("/", async (int projectId, ProjectDocumentQueryService service, CancellationToken ct) =>
        {
            var result = await service.ListAsync(projectId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapGet("/{documentId:int}", async (int projectId, int documentId,
            ProjectDocumentQueryService service, CancellationToken ct) =>
        {
            var result = await service.GetAsync(projectId, documentId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapGet("/{documentId:int}/content", async (int projectId, int documentId,
            ProjectDocumentQueryService service, HttpContext context, CancellationToken ct) =>
            await OpenAsync(service, projectId, documentId, null, context, inline: false, ct));
        group.MapGet("/{documentId:int}/preview", async (int projectId, int documentId,
            ProjectDocumentQueryService service, HttpContext context, CancellationToken ct) =>
            await OpenAsync(service, projectId, documentId, null, context, inline: true, ct));
        group.MapGet("/{documentId:int}/versions/{versionId:int}/content", async
            (int projectId, int documentId, int versionId, ProjectDocumentQueryService service,
             HttpContext context, CancellationToken ct) =>
            await OpenAsync(service, projectId, documentId, versionId, context, inline: false, ct));

        group.MapPost("/", async (int projectId, HttpRequest request, ClaimsPrincipal user,
            ProjectDocumentService service, CancellationToken ct) =>
        {
            var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actor)) return Results.Unauthorized();
            if (!request.HasFormContentType) return Results.BadRequest("Se requiere multipart/form-data.");
            if (request.ContentLength > 21L * 1024 * 1024) return Results.StatusCode(413);
            try
            {
                // Explicit limits apply even if Content-Length is omitted.
                var form = await request.ReadFormAsync(ct);
                if (form.Files.Count != 1 || form.Files[0].Length == 0 ||
                    form.Files[0].Length > ProjectDocumentUploadPolicy.MaxUploadBytes)
                    return Results.BadRequest("Adjunte exactamente un archivo válido de hasta 20 MB.");
                var displayName = form["displayName"].ToString();
                var category = form["category"].ToString();
                var file = form.Files[0];
                await using var raw = file.OpenReadStream();
                var verified = await ProjectDocumentUploadPolicy.ValidateAsync(raw, file.FileName, ct);
                await using var content = new MemoryStream(verified.Bytes, writable:false);
                var result = await service.CreateAsync(projectId, displayName, category,
                    file.FileName, verified.ContentType, content, actor,
                    user.IsInRole(RoleNames.Administrator), ct);
                return Results.Created($"/api/projects/{projectId}/documents/{result.Id}", result);
            }
            catch (ProjectOperationException e) { return Problem(e); }
            catch (InvalidDataException e) { return Results.BadRequest(e.Message); }
            catch (BadHttpRequestException) { return Results.BadRequest("Carga multipart inválida."); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>();
        ProjectDocumentLifecycleEndpoints.Map(group);
        return endpoints;
    }

    private static async Task<IResult> OpenAsync(ProjectDocumentQueryService service,
        int projectId, int documentId, int? versionId, HttpContext context,
        bool inline, CancellationToken ct)
    {
        try
        {
            var file = await service.OpenAsync(projectId, documentId, versionId, ct);
            if (file is null) return Results.NotFound();
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Content-Security-Policy"] = "sandbox";
            var previewable = file.ContentType is "application/pdf" or "image/jpeg" or "image/png" or "image/webp";
            if (inline && previewable)
            {
                context.Response.Headers.ContentDisposition = "inline";
                return Results.Stream(file.Content, file.ContentType);
            }
            return Results.File(file.Content, "application/octet-stream",
                fileDownloadName: file.FileName);
        }
        catch (FileNotFoundException) { return Results.NotFound(); }
        catch (DirectoryNotFoundException) { return Results.NotFound(); }
    }

    private static IResult Problem(ProjectOperationException ex) => Results.Problem(
        title: ex.Message, statusCode: ex.Reason switch
        {
            ProjectFailureReason.Forbidden => StatusCodes.Status403Forbidden,
            ProjectFailureReason.ConcurrencyConflict or ProjectFailureReason.Duplicate => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        });
}
