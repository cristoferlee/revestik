using Microsoft.EntityFrameworkCore;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.Projects.Pdf;
using Revestik.Shared.Projects;

namespace Revestik.Api.Endpoints;

public static class ProjectReportEndpoints
{
    public static IEndpointRouteBuilder MapProjectReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var projects = endpoints.MapGroup("/api/projects/{projectId:int}")
            .WithTags("Project reports").RequireAuthorization(PolicyNames.ManageProjects);

        projects.MapGet("/report.pdf", async (int projectId, ProjectReportPdfService report, HttpContext context,
            CancellationToken ct) =>
        {
            var pdf = await report.GenerateAsync(projectId, ct);
            if (pdf is null) return Results.NotFound();
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(pdf, "application/pdf", $"proyecto-{projectId}-expediente.pdf");
        });

        projects.MapGet("/document-links", async (int projectId, RevestikDbContext db, CancellationToken ct) =>
        {
            if (!await db.Set<Project>().AsNoTracking().AnyAsync(x => x.Id == projectId, ct))
                return Results.NotFound();
            var links = await db.Set<ProjectDocumentEventLink>().AsNoTracking()
                .Where(x => x.ProjectId == projectId && x.Document.ProjectId == projectId &&
                    x.Document.DeletedAtUtc == null && x.Event.ProjectId == projectId && !x.Event.IsVoided)
                .OrderBy(x => x.ProjectDocumentId).ThenBy(x => x.ProjectEventId)
                .Select(x => new ProjectDocumentLinkResponse(x.ProjectDocumentId, x.ProjectEventId, x.LinkedAtUtc))
                .ToListAsync(ct);
            return Results.Ok(links);
        });
        return endpoints;
    }
}
