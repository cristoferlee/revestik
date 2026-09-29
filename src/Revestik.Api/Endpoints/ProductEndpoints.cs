using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Services.Inventory;
using Revestik.Api.Services.Products;
using Revestik.Shared.Inventory;
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

        group.MapPost(
            "/with-initial-stock",
            async (
                InventoryProductCreateRequest request,
                ClaimsPrincipal user,
                RevestikDbContext dbContext,
                IProductService productService,
                IInventoryService inventoryService,
                CancellationToken cancellationToken) =>
            {
                var validationErrors =
                    MergeValidationErrors(
                        ValidateRequest(request.Product),
                        ValidateRequest(request.InitialStock));

                if (validationErrors.Count > 0)
                {
                    return Results.ValidationProblem(validationErrors);
                }

                var userId =
                    user.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Results.Unauthorized();
                }

                await using var transaction =
                    await dbContext.Database.BeginTransactionAsync(
                        cancellationToken);

                try
                {
                    var product = await productService.CreateAsync(
                        request.Product,
                        cancellationToken);

                    await inventoryService.RegisterInitialStockAsync(
                        product.Id,
                        request.InitialStock,
                        userId,
                        cancellationToken);

                    var createdProduct =
                        await productService.GetByIdAsync(
                            product.Id,
                            cancellationToken)
                        ?? throw new InvalidOperationException(
                            "The created product could not be loaded.");

                    await transaction.CommitAsync(cancellationToken);

                    return Results.Created(
                        $"/api/products/{createdProduct.Id}",
                        createdProduct);
                }
                catch (InvalidProductCatalogReferenceException exception)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return CreateCatalogReferenceProblem(exception.Message);
                }
            })
            .RequireAuthorization(PolicyNames.ManageProductCatalog)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("CreateInventoryProduct");

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

    private static Dictionary<string, string[]> MergeValidationErrors(
        params Dictionary<string, string[]>[] collections)
    {
        return collections
            .SelectMany(collection => collection)
            .GroupBy(pair => pair.Key)
            .ToDictionary(
                group => group.Key,
                group => group
                    .SelectMany(pair => pair.Value)
                    .Distinct()
                    .ToArray());
    }
}