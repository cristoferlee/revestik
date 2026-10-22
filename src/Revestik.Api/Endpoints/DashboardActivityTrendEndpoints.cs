using Revestik.Api.Services.Sales;

namespace Revestik.Api.Endpoints;

public static class DashboardActivityTrendEndpoints
{
    public static IEndpointRouteBuilder MapDashboardActivityTrendEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard/activity-trend", async (int year, int month,
            DashboardActivityTrendService service, CancellationToken ct) =>
        {
            if (year is < 2 or > 9998 || month is < 1 or > 12)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["period"] = ["Período inválido."] });
            return Results.Ok(await service.GetAsync(year, month, ct));
        })
        .WithName("GetDashboardActivityTrend")
        .WithTags("Dashboard")
        .RequireAuthorization();
        return app;
    }
}
