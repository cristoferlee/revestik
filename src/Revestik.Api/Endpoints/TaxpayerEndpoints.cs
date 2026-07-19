using Revestik.Api.Integrations.Hacienda;
using Revestik.Shared.Taxpayers;

namespace Revestik.Api.Endpoints;

public static class TaxpayerEndpoints
{
    public static IEndpointRouteBuilder MapTaxpayerEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/taxpayers")
            .WithTags("Taxpayers");

        group.MapGet(
                "/{identificationNumber}",
                FindTaxpayerAsync)
            .WithName("FindTaxpayer")
            .Produces<TaxpayerLookupResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> FindTaxpayerAsync(
        string identificationNumber,
        IHaciendaTaxpayerClient taxpayerClient,
        CancellationToken cancellationToken)
    {
        var normalizedIdentification = identificationNumber.Trim();

        if (!IsValidIdentificationFormat(normalizedIdentification))
        {
            return Results.Problem(
                title: "Invalid identification number.",
                detail:
                    "The identification number must contain between 9 and 12 digits without hyphens.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await taxpayerClient.FindAsync(
            normalizedIdentification,
            cancellationToken);

        return result.Status switch
        {
            TaxpayerLookupStatus.Found =>
                Results.Ok(CreateResponse(
                    normalizedIdentification,
                    result.Taxpayer!)),

            TaxpayerLookupStatus.NotFound =>
                Results.Problem(
                    title: "Taxpayer not found.",
                    detail:
                        "No taxpayer was found for the supplied identification number.",
                    statusCode: StatusCodes.Status404NotFound),

            TaxpayerLookupStatus.RateLimited =>
                Results.Problem(
                    title: "Hacienda request limit reached.",
                    detail:
                        "The taxpayer service is temporarily limiting requests. Try again later.",
                    statusCode: StatusCodes.Status429TooManyRequests),

            _ =>
                Results.Problem(
                    title: "Hacienda service unavailable.",
                    detail:
                        "The taxpayer service could not be reached. Try again later.",
                    statusCode: StatusCodes.Status503ServiceUnavailable)
        };
    }

    private static bool IsValidIdentificationFormat(
        string identificationNumber)
    {
        return identificationNumber.Length is >= 9 and <= 12 &&
               identificationNumber.All(char.IsDigit);
    }

    private static TaxpayerLookupResponse CreateResponse(
        string identificationNumber,
        HaciendaTaxpayer taxpayer)
    {
        return new TaxpayerLookupResponse(
            identificationNumber,
            taxpayer.IdentificationTypeCode,
            taxpayer.Name,
            taxpayer.TaxRegime?.Description ?? string.Empty,
            taxpayer.TaxStatus?.Status ?? string.Empty,
            IsAffirmative(taxpayer.TaxStatus?.Delinquent),
            IsAffirmative(taxpayer.TaxStatus?.NonFiler),
            taxpayer.TaxStatus?.TaxAdministration ?? string.Empty);
    }

    private static bool IsAffirmative(string? value)
    {
        return string.Equals(
            value,
            "SI",
            StringComparison.OrdinalIgnoreCase);
    }
}