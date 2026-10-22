using Revestik.Api.Authorization;
using Revestik.Api.Services.Projects;
namespace Revestik.Api.Endpoints;
public static class ProjectDashboardAlertEndpoints
{
    public static IEndpointRouteBuilder MapProjectDashboardAlertEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/dashboard/project-alerts", async (ProjectDeadlineQueryService service, CancellationToken ct) =>
            Results.Ok(await service.GetAlertsAsync(DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("America/Costa_Rica"))), ct)))
            .WithTags("Dashboard").RequireAuthorization(PolicyNames.ManageProjects);
        endpoints.MapGet("/api/dashboard/projects-summary", async (
            ProjectDashboardSummaryService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(ct)))
            .WithTags("Dashboard")
            .RequireAuthorization();
        return endpoints;
    }
}
