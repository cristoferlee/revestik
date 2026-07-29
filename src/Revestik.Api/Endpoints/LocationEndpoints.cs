using Revestik.Api.Integrations.Locations;
using Revestik.Shared.Locations;

namespace Revestik.Api.Endpoints;

public static class LocationEndpoints
{
    public static IEndpointRouteBuilder MapLocationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/locations")
            .WithTags("Locations")
            .RequireAuthorization();

        group.MapGet(
                "/provinces",
                GetProvincesAsync)
            .WithName("GetProvinces")
            .Produces<IReadOnlyList<LocationOptionResponse>>()
            .ProducesProblem(
                StatusCodes.Status503ServiceUnavailable);

        group.MapGet(
                "/provinces/{provinceCode}/cantons",
                GetCantonsAsync)
            .WithName("GetCantons")
            .Produces<IReadOnlyList<LocationOptionResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(
                StatusCodes.Status503ServiceUnavailable);

        group.MapGet(
                "/provinces/{provinceCode}/cantons/{cantonCode}/districts",
                GetDistrictsAsync)
            .WithName("GetDistricts")
            .Produces<IReadOnlyList<LocationOptionResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(
                StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> GetProvincesAsync(
        ILocationCatalogClient locationCatalogClient,
        CancellationToken cancellationToken)
    {
        var result = await locationCatalogClient.GetProvincesAsync(
            cancellationToken);

        return ToHttpResult(result);
    }

    private static async Task<IResult> GetCantonsAsync(
        string provinceCode,
        ILocationCatalogClient locationCatalogClient,
        CancellationToken cancellationToken)
    {
        if (!IsValidCode(provinceCode, 1))
        {
            return InvalidCode(
                "Province code must contain one digit.");
        }

        var result = await locationCatalogClient.GetCantonsAsync(
            provinceCode,
            cancellationToken);

        return ToHttpResult(result);
    }

    private static async Task<IResult> GetDistrictsAsync(
        string provinceCode,
        string cantonCode,
        ILocationCatalogClient locationCatalogClient,
        CancellationToken cancellationToken)
    {
        if (!IsValidCode(provinceCode, 1))
        {
            return InvalidCode(
                "Province code must contain one digit.");
        }

        if (!IsValidCode(cantonCode, 2))
        {
            return InvalidCode(
                "Canton code must contain two digits.");
        }

        var result = await locationCatalogClient.GetDistrictsAsync(
            provinceCode,
            cantonCode,
            cancellationToken);

        return ToHttpResult(result);
    }

    private static IResult ToHttpResult(
        LocationCatalogResult result)
    {
        return result.Status switch
        {
            LocationCatalogStatus.Available =>
                Results.Ok(result.Locations),

            LocationCatalogStatus.NotFound =>
                Results.Problem(
                    title: "Locations not found.",
                    statusCode: StatusCodes.Status404NotFound),

            _ =>
                Results.Problem(
                    title: "Location catalog unavailable.",
                    detail:
                        "The location catalog could not be reached. Try again later.",
                    statusCode:
                        StatusCodes.Status503ServiceUnavailable)
        };
    }

    private static bool IsValidCode(
        string code,
        int requiredLength)
    {
        return code.Length == requiredLength &&
               code.All(char.IsDigit);
    }

    private static IResult InvalidCode(string detail)
    {
        return Results.Problem(
            title: "Invalid location code.",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);
    }
}