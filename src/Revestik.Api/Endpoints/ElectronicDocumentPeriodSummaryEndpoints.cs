using System.ComponentModel.DataAnnotations;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Endpoints;

public static class ElectronicDocumentPeriodSummaryEndpoints
{
    public static IEndpointRouteBuilder MapElectronicDocumentPeriodSummaryEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/purchases/received-documents/summary-period",
                async (
                    [AsParameters] ElectronicDocumentSummaryRequest request,
                    RevestikDbContext dbContext,
                    CancellationToken cancellationToken) =>
                {
                    var errors = ValidateRequest(request);
                    if (errors.Count > 0)
                    {
                        return Results.ValidationProblem(errors);
                    }

                    var service = new ElectronicDocumentPeriodSummaryService(dbContext);
                    return Results.Ok(await service.GetSummaryAsync(request, cancellationToken));
                })
            .RequireAuthorization(PolicyNames.ManagePurchases)
            .WithTags("Received documents")
            .WithName("GetReceivedElectronicDocumentPeriodSummary");

        return endpoints;
    }

    private static Dictionary<string, string[]> ValidateRequest<TRequest>(TRequest request)
        where TRequest : class
    {
        var validationResults = new List<ValidationResult>();

        Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        return validationResults
            .SelectMany(result =>
                result.MemberNames
                    .DefaultIfEmpty("request")
                    .Select(memberName => new
                    {
                        MemberName = memberName,
                        ErrorMessage = result.ErrorMessage ?? "Invalid value."
                    }))
            .GroupBy(error => error.MemberName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());
    }
}
