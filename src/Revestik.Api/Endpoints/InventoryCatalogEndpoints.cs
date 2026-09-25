using System.ComponentModel.DataAnnotations;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Products;
using Revestik.Shared.Products;

namespace Revestik.Api.Endpoints;

public static class InventoryCatalogEndpoints
{
    public static IEndpointRouteBuilder MapInventoryCatalogEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        MapCategoryEndpoints(endpoints);
        MapUnitEndpoints(endpoints);

        return endpoints;
    }

    private static void MapCategoryEndpoints(
        IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/product-categories")
            .WithTags("Product Categories")
            .RequireAuthorization(PolicyNames.ManageInventory);

        group.MapGet(
                "/",
                async (
                    bool? includeInactive,
                    IInventoryCatalogService service,
                    CancellationToken cancellationToken) =>
                {
                    var categories = await service.GetCategoriesAsync(
                        includeInactive ?? false,
                        cancellationToken);

                    return Results.Ok(categories);
                })
            .WithName("GetProductCategories");

        group.MapGet(
                "/{id:int}",
                async (
                    int id,
                    IInventoryCatalogService service,
                    CancellationToken cancellationToken) =>
                {
                    var category = await service.GetCategoryByIdAsync(
                        id,
                        cancellationToken);

                    return category is null
                        ? Results.NotFound()
                        : Results.Ok(category);
                })
            .WithName("GetProductCategoryById");

        group.MapPost(
                "/",
                async (
                    ProductCategoryUpsertRequest request,
                    IInventoryCatalogService service,
                    CancellationToken cancellationToken) =>
                {
                    var validationErrors = ValidateRequest(request);

                    if (validationErrors.Count > 0)
                    {
                        return Results.ValidationProblem(validationErrors);
                    }

                    try
                    {
                        var category = await service.CreateCategoryAsync(
                            request,
                            cancellationToken);

                        return Results.Created(
                            $"/api/product-categories/{category.Id}",
                            category);
                    }
                    catch (DuplicateInventoryCatalogValueException exception)
                    {
                        return CreateConflict(exception.Message);
                    }
                })
            .RequireAuthorization(PolicyNames.ManageProductCatalog)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("CreateProductCategory");

        group.MapPut(
                "/{id:int}",
                async (
                    int id,
                    ProductCategoryUpsertRequest request,
                    IInventoryCatalogService service,
                    CancellationToken cancellationToken) =>
                {
                    var validationErrors = ValidateRequest(request);

                    if (validationErrors.Count > 0)
                    {
                        return Results.ValidationProblem(validationErrors);
                    }

                    try
                    {
                        var category = await service.UpdateCategoryAsync(
                            id,
                            request,
                            cancellationToken);

                        return category is null
                            ? Results.NotFound()
                            : Results.Ok(category);
                    }
                    catch (DuplicateInventoryCatalogValueException exception)
                    {
                        return CreateConflict(exception.Message);
                    }
                })
            .RequireAuthorization(PolicyNames.ManageProductCatalog)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("UpdateProductCategory");

        group.MapDelete(
                "/{id:int}",
                async (
                    int id,
                    IInventoryCatalogService service,
                    CancellationToken cancellationToken) =>
                {
                    var wasDeactivated =
                        await service.DeactivateCategoryAsync(
                            id,
                            cancellationToken);

                    return wasDeactivated
                        ? Results.NoContent()
                        : Results.NotFound();
                })
            .RequireAuthorization(PolicyNames.ManageProductCatalog)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("DeactivateProductCategory");
    }

    private static void MapUnitEndpoints(
        IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/units-of-measure")
            .WithTags("Units of Measure")
            .RequireAuthorization(PolicyNames.ManageInventory);

        group.MapGet(
                "/",
                async (
                    bool? includeInactive,
                    IInventoryCatalogService service,
                    CancellationToken cancellationToken) =>
                {
                    var units = await service.GetUnitsAsync(
                        includeInactive ?? false,
                        cancellationToken);

                    return Results.Ok(units);
                })
            .WithName("GetUnitsOfMeasure");

        group.MapGet(
                "/{id:int}",
                async (
                    int id,
                    IInventoryCatalogService service,
                    CancellationToken cancellationToken) =>
                {
                    var unit = await service.GetUnitByIdAsync(
                        id,
                        cancellationToken);

                    return unit is null
                        ? Results.NotFound()
                        : Results.Ok(unit);
                })
            .WithName("GetUnitOfMeasureById");

        group.MapPost(
                "/",
                async (
                    UnitOfMeasureUpsertRequest request,
                    IInventoryCatalogService service,
                    CancellationToken cancellationToken) =>
                {
                    var validationErrors = ValidateRequest(request);

                    if (validationErrors.Count > 0)
                    {
                        return Results.ValidationProblem(validationErrors);
                    }

                    try
                    {
                        var unit = await service.CreateUnitAsync(
                            request,
                            cancellationToken);

                        return Results.Created(
                            $"/api/units-of-measure/{unit.Id}",
                            unit);
                    }
                    catch (DuplicateInventoryCatalogValueException exception)
                    {
                        return CreateConflict(exception.Message);
                    }
                })
            .RequireAuthorization(PolicyNames.ManageProductCatalog)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("CreateUnitOfMeasure");

        group.MapPut(
                "/{id:int}",
                async (
                    int id,
                    UnitOfMeasureUpsertRequest request,
                    IInventoryCatalogService service,
                    CancellationToken cancellationToken) =>
                {
                    var validationErrors = ValidateRequest(request);

                    if (validationErrors.Count > 0)
                    {
                        return Results.ValidationProblem(validationErrors);
                    }

                    try
                    {
                        var unit = await service.UpdateUnitAsync(
                            id,
                            request,
                            cancellationToken);

                        return unit is null
                            ? Results.NotFound()
                            : Results.Ok(unit);
                    }
                    catch (DuplicateInventoryCatalogValueException exception)
                    {
                        return CreateConflict(exception.Message);
                    }
                })
            .RequireAuthorization(PolicyNames.ManageProductCatalog)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("UpdateUnitOfMeasure");

        group.MapDelete(
                "/{id:int}",
                async (
                    int id,
                    IInventoryCatalogService service,
                    CancellationToken cancellationToken) =>
                {
                    var wasDeactivated = await service.DeactivateUnitAsync(
                        id,
                        cancellationToken);

                    return wasDeactivated
                        ? Results.NoContent()
                        : Results.NotFound();
                })
            .RequireAuthorization(PolicyNames.ManageProductCatalog)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("DeactivateUnitOfMeasure");
    }

    private static IResult CreateConflict(string detail)
    {
        return Results.Problem(
            title: "Valor duplicado.",
            detail: detail,
            statusCode: StatusCodes.Status409Conflict);
    }

    private static Dictionary<string, string[]> ValidateRequest<TRequest>(
        TRequest request)
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
                group => group
                    .Select(error => error.ErrorMessage)
                    .ToArray());
    }
}