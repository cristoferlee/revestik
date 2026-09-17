using System.ComponentModel.DataAnnotations;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Quotations;
using Revestik.Shared.Quotations;

namespace Revestik.Api.Endpoints;

public static class QuotationEndpoints
{
    public static IEndpointRouteBuilder MapQuotationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/quotations")
            .WithTags("Quotations")
            .RequireAuthorization(PolicyNames.ManageQuotations);

        group.MapGet(
            "/{id:int}",
            async (
                int id,
                IQuotationService quotationService,
                CancellationToken cancellationToken) =>
            {
                var quotation = await quotationService.GetByIdAsync(
                    id,
                    cancellationToken);

                return quotation is null
                    ? Results.NotFound()
                    : Results.Ok(quotation);
            })
            .WithName("GetQuotationById");

        group.MapPost(
            "/",
            async (
                QuotationUpsertRequest request,
                IQuotationService quotationService,
                CancellationToken cancellationToken) =>
            {
                var validationErrors = ValidateRequest(request);

                if (validationErrors.Count > 0)
                {
                    return Results.ValidationProblem(validationErrors);
                }

                try
                {
                    var quotation = await quotationService.CreateAsync(
                        request,
                        cancellationToken);

                    return Results.Created(
                        $"/api/quotations/{quotation.Id}",
                        quotation);
                }
                catch (InvalidOperationException exception)
                    when (exception.Message ==
                        "The customer does not exist or is inactive.")
                {
                    return Results.Problem(
                        title: "Invalid customer.",
                        detail:
                            "The customer does not exist or is inactive.",
                        statusCode: StatusCodes.Status400BadRequest);
                }
            })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("CreateQuotation");

        group.MapPut(
            "/{id:int}",
            async (
                int id,
                QuotationUpsertRequest request,
                IQuotationService quotationService,
                CancellationToken cancellationToken) =>
            {
                var validationErrors = ValidateRequest(request);

                if (validationErrors.Count > 0)
                {
                    return Results.ValidationProblem(validationErrors);
                }

                try
                {
                    var quotation = await quotationService.UpdateAsync(
                        id,
                        request,
                        cancellationToken);

                    return quotation is null
                        ? Results.NotFound()
                        : Results.Ok(quotation);
                }
                catch (InvalidOperationException exception)
                    when (exception.Message ==
                        "The customer does not exist or is inactive.")
                {
                    return Results.Problem(
                        title: "Invalid customer.",
                        detail:
                            "The customer does not exist or is inactive.",
                        statusCode: StatusCodes.Status400BadRequest);
                }
            })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("UpdateQuotation");

        return endpoints;
    }

    private static Dictionary<string, string[]> ValidateRequest<TRequest>(
        TRequest request)
        where TRequest : class
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(request);

        Validator.TryValidateObject(
            request,
            validationContext,
            validationResults,
            validateAllProperties: true);

        return validationResults
            .SelectMany(result =>
                result.MemberNames
                    .DefaultIfEmpty("request")
                    .Select(memberName => new
                    {
                        MemberName = memberName,
                        ErrorMessage =
                            result.ErrorMessage ?? "Invalid value."
                    }))
            .GroupBy(error => error.MemberName)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(error => error.ErrorMessage)
                    .ToArray());
    }
}