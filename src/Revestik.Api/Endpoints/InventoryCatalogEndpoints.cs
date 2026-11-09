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
        MapSubcategoryEndpoints(endpoints);
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

    private static void MapSubcategoryEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/product-subcategories")
            .WithTags("Product Subcategories")
            .RequireAuthorization(PolicyNames.ManageInventory);

        group.MapGet("/", async (bool? includeInactive,
            IInventoryCatalogService service, CancellationToken ct) =>
            Results.Ok(await service.GetSubcategoriesAsync(includeInactive ?? false, ct)))
            .WithName("GetProductSubcategories");

        group.MapGet("/{id:int}", async (int id, IInventoryCatalogService service,
            CancellationToken ct) =>
        {
            var item = await service.GetSubcategoryByIdAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).WithName("GetProductSubcategoryById");

        group.MapPost("/", async (ProductSubcategoryUpsertRequest request,
            IInventoryCatalogService service, CancellationToken ct) =>
        {
            var errors = ValidateRequest(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            try
            {
                var item = await service.CreateSubcategoryAsync(request, ct);
                return Results.Created($"/api/product-subcategories/{item.Id}", item);
            }
            catch (DuplicateInventoryCatalogValueException e) { return CreateConflict(e.Message); }
            catch (InvalidProductCatalogReferenceException e) { return Results.BadRequest(e.Message); }
        }).RequireAuthorization(PolicyNames.ManageProductCatalog)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("CreateProductSubcategory");

        group.MapPut("/{id:int}", async (int id, ProductSubcategoryWriteRequest request,
            IInventoryCatalogService service, CancellationToken ct) =>
        {
            var errors = ValidateRequest(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            if (string.IsNullOrWhiteSpace(request.Version)) return Results.BadRequest("Se requiere versión.");
            try
            {
                var item = await service.UpdateSubcategoryAsync(id, request, request.Version, ct);
                return item is null ? Results.NotFound() : Results.Ok(item);
            }
            catch (DuplicateInventoryCatalogValueException e) { return CreateConflict(e.Message); }
            catch (InvalidProductCatalogReferenceException e) { return Results.BadRequest(e.Message); }
            catch (InvalidOperationException e) { return Results.Conflict(e.Message); }
        }).RequireAuthorization(PolicyNames.ManageProductCatalog)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("UpdateProductSubcategory");

        group.MapDelete("/{id:int}", async (int id, string version,
            IInventoryCatalogService service, CancellationToken ct) =>
        {
            try
            {
                return await service.DeactivateSubcategoryAsync(id, version, ct)
                    ? Results.NoContent() : Results.NotFound();
            }
            catch (InvalidOperationException e) { return Results.Conflict(e.Message); }
        }).RequireAuthorization(PolicyNames.ManageProductCatalog)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("DeactivateProductSubcategory");
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