using Revestik.Api.Services.Expenses;

namespace Revestik.Api.Endpoints;

public static class OperatingResultDashboardEndpoints
{
    public static IEndpointRouteBuilder MapOperatingResultDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard/operating-result-summary", async (
            OperatingResultSummaryService service, CancellationToken ct) =>
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Costa_Rica");
            var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            return Results.Ok(await service.GetAsync(today.Year, today.Month, ct));
        })
        .WithName("GetDashboardOperatingResultSummary")
        .WithTags("Dashboard")
        .RequireAuthorization();
        return app;
    }
}
