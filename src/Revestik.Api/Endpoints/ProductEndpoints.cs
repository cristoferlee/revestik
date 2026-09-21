using System.ComponentModel.DataAnnotations;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Products;
using Revestik.Shared.Products;

namespace Revestik.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/products")
            .WithTags("Products")
            .RequireAuthorization(PolicyNames.ManageQuotations);

        group.MapGet(
            "/",
            async (
                [AsParameters] ProductListRequest request,
                IProductService productService,
                CancellationToken cancellationToken) =>
            {
                var validationErrors = ValidateRequest(request);

                if (validationErrors.Count > 0)
                {
                    return Results.ValidationProblem(validationErrors);
                }

                var products = await productService.GetPageAsync(
                    request,
                    cancellationToken);

                return Results.Ok(products);
            })
            .WithName("GetProducts");

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