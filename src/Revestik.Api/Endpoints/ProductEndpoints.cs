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
            .RequireAuthorization(PolicyNames.ManageInventory);

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

        group.MapGet(
            "/{id:int}",
            async (
                int id,
                IProductService productService,
                CancellationToken cancellationToken) =>
            {
                var product = await productService.GetByIdAsync(
                    id,
                    cancellationToken);

                return product is null
                    ? Results.NotFound()
                    : Results.Ok(product);
            })
            .WithName("GetProductById");

        group.MapPost(
            "/",
            async (
                ProductUpsertRequest request,
                IProductService productService,
                CancellationToken cancellationToken) =>
            {
                var validationErrors = ValidateRequest(request);

                if (validationErrors.Count > 0)
                {
                    return Results.ValidationProblem(validationErrors);
                }

                try
                {
                    var product = await productService.CreateAsync(
                        request,
                        cancellationToken);

                    return Results.Created(
                        $"/api/products/{product.Id}",
                        product);
                }
                catch (InvalidProductCatalogReferenceException exception)
                {
                    return CreateCatalogReferenceProblem(exception.Message);
                }
            })
            .RequireAuthorization(PolicyNames.ManageProductCatalog)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("CreateProduct");

        group.MapPut(
            "/{id:int}",
            async (
                int id,
                ProductUpsertRequest request,
                IProductService productService,
                CancellationToken cancellationToken) =>
            {
                var validationErrors = ValidateRequest(request);

                if (validationErrors.Count > 0)
                {
                    return Results.ValidationProblem(validationErrors);
                }

                try
                {
                    var product = await productService.UpdateAsync(
                        id,
                        request,
                        cancellationToken);

                    return product is null
                        ? Results.NotFound()
                        : Results.Ok(product);
                }
                catch (InvalidProductCatalogReferenceException exception)
                {
                    return CreateCatalogReferenceProblem(exception.Message);
                }
            })
            .RequireAuthorization(PolicyNames.ManageProductCatalog)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("UpdateProduct");

        group.MapDelete(
            "/{id:int}",
            async (
                int id,
                IProductService productService,
                CancellationToken cancellationToken) =>
            {
                var wasDeleted = await productService.DeleteAsync(
                    id,
                    cancellationToken);

                return wasDeleted
                    ? Results.NoContent()
                    : Results.NotFound();
            })
            .RequireAuthorization(PolicyNames.AdministratorOnly)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("DeleteProduct");

        group.MapPost(
            "/{id:int}/reactivate",
            async (
                int id,
                IProductService productService,
                CancellationToken cancellationToken) =>
            {
                var wasReactivated = await productService.ReactivateAsync(
                    id,
                    cancellationToken);

                return wasReactivated
                    ? Results.NoContent()
                    : Results.NotFound();
            })
            .RequireAuthorization(PolicyNames.AdministratorOnly)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("ReactivateProduct");

        return endpoints;
    }

    private static IResult CreateCatalogReferenceProblem(string detail)
    {
        return Results.Problem(
            title: "Referencia de catálogo inválida.",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);
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