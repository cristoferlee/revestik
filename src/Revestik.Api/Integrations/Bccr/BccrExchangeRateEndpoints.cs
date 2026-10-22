using Revestik.Api.Integrations.Bccr;

namespace Revestik.Api.Endpoints;

public static class BccrExchangeRateEndpoints
{
    public static IEndpointRouteBuilder MapBccrExchangeRateEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard/exchange-rate", async (BccrExchangeRateService service, CancellationToken ct) =>
        {
            var rate = await service.GetAsync(ct);
            return rate is null ? Results.Problem("El tipo de cambio BCCR no está disponible o la integración no está configurada.", statusCode: 503) : Results.Ok(rate);
        })
        .RequireAuthorization()
        .WithName("GetDashboardBccrExchangeRate")
        .WithTags("Dashboard");
        return app;
    }
}
