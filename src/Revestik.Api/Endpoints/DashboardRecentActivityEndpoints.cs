using System.Security.Claims;
using Revestik.Api.Services.Dashboard;

namespace Revestik.Api.Endpoints;

public static class DashboardRecentActivityEndpoints
{
    public static IEndpointRouteBuilder MapDashboardRecentActivityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard/recent-activity", async (
            int? limit, ClaimsPrincipal user, DashboardRecentActivityService service, CancellationToken ct) =>
        {
            if (limit is < 1 or > 20)
                return Results.ValidationProblem(new Dictionary<string, string[]> {
                    ["limit"] = ["El límite permitido es de 1 a 20."] });
            return Results.Ok(await service.GetAsync(user, limit ?? 5, ct));
        })
        .WithName("GetDashboardRecentActivity")
        .WithTags("Dashboard")
        .RequireAuthorization();
        return app;
    }
}
