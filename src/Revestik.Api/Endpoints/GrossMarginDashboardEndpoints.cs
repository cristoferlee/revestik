using Revestik.Api.Services.Sales;

namespace Revestik.Api.Endpoints;

public static class GrossMarginDashboardEndpoints
{
    public static IEndpointRouteBuilder MapGrossMarginDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard/gross-margin-summary", async (
            GrossMarginSummaryService service, CancellationToken ct) =>
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Costa_Rica");
            var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            return Results.Ok(await service.GetAsync(today.Year, today.Month, ct));
        })
        .WithName("GetDashboardGrossMarginSummary")
        .WithTags("Dashboard")
        .RequireAuthorization();
        return app;
    }
}
