using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Inventory;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Endpoints;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/inventory")
            .WithTags("Inventory")
            .RequireAuthorization(PolicyNames.ManageInventory);

        group.MapGet(
                "/summary",
                async (
                    IInventoryService inventoryService,
                    CancellationToken cancellationToken) =>
                {
                    var summary =
                        await inventoryService.GetSummaryAsync(
                            cancellationToken);

                    return Results.Ok(summary);
                })
            .WithName("GetInventorySummary");

        group.MapPost(
                "/products/{productId:int}/initial-stock",
                async (
                    int productId,
                    InitialStockRequest request,
                    ClaimsPrincipal user,
                    IInventoryService inventoryService,
                    CancellationToken cancellationToken) =>
                {
                    var validationErrors = ValidateRequest(request);

                    if (validationErrors.Count > 0)
                    {
                        return Results.ValidationProblem(validationErrors);
                    }

                    var createdByUserId = GetUserId(user);

                    if (createdByUserId is null)
                    {
                        return CreateInvalidUserProblem();
                    }

                    try
                    {
                        var movement =
                            await inventoryService.RegisterInitialStockAsync(
                                productId,
                                request,
                                createdByUserId,
                                cancellationToken);

                        return movement is null
                            ? Results.NotFound()
                            : Results.Ok(movement);
                    }
                    catch (InitialStockAlreadyRegisteredException exception)
                    {
                        return Results.Problem(
                            title: "Initial stock already registered.",
                            detail: exception.Message,
                            statusCode: StatusCodes.Status409Conflict);
                    }
                    catch (InventoryConcurrencyException exception)
                    {
                        return CreateConcurrencyProblem(exception.Message);
                    }
                    catch (InvalidInventoryOperationException exception)
                    {
                        return CreateInvalidOperationProblem(exception.Message);
                    }
                    catch (InvalidInventoryUserException exception)
                    {
                        return CreateInvalidUserProblem(exception.Message);
                    }
                })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("RegisterInitialStock");

        group.MapPost(
                "/products/{productId:int}/adjustments",
                async (
                    int productId,
                    InventoryAdjustmentRequest request,
                    ClaimsPrincipal user,
                    IInventoryService inventoryService,
                    CancellationToken cancellationToken) =>
                {
                    var validationErrors = ValidateRequest(request);

                    if (validationErrors.Count > 0)
                    {
                        return Results.ValidationProblem(validationErrors);
                    }

                    var createdByUserId = GetUserId(user);

                    if (createdByUserId is null)
                    {
                        return CreateInvalidUserProblem();
                    }

                    try
                    {
                        var movement =
                            await inventoryService.AdjustStockAsync(
                                productId,
                                request,
                                createdByUserId,
                                cancellationToken);

                        return movement is null
                            ? Results.NotFound()
                            : Results.Ok(movement);
                    }
                    catch (InventoryConcurrencyException exception)
                    {
                        return CreateConcurrencyProblem(exception.Message);
                    }
                    catch (InvalidInventoryOperationException exception)
                    {
                        return CreateInvalidOperationProblem(exception.Message);
                    }
                    catch (InvalidInventoryUserException exception)
                    {
                        return CreateInvalidUserProblem(exception.Message);
                    }
                })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("AdjustInventoryStock");

        group.MapPost(
                "/physical-counts",
                async (
                    PhysicalCountCreateRequest request,
                    ClaimsPrincipal user,
                    IInventoryPhysicalCountService physicalCountService,
                    CancellationToken cancellationToken) =>
                {
                    var validationErrors = ValidateRequest(request);

                    if (validationErrors.Count > 0)
                    {
                        return Results.ValidationProblem(validationErrors);
                    }

                    var userId = GetUserId(user);

                    if (userId is null)
                    {
                        return CreateInvalidUserProblem();
                    }

                    try
                    {
                        var physicalCount =
                            await physicalCountService.StartAsync(
                                request,
                                userId,
                                cancellationToken);

                        return Results.Created(
                            $"/api/inventory/physical-counts/{physicalCount.Id}",
                            physicalCount);
                    }
                    catch (InvalidInventoryOperationException exception)
                    {
                        return CreateInvalidOperationProblem(exception.Message);
                    }
                    catch (InvalidInventoryUserException exception)
                    {
                        return CreateInvalidUserProblem(exception.Message);
                    }
                })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("StartInventoryPhysicalCount");

        group.MapGet(
                "/physical-counts/{id:int}",
                async (
                    int id,
                    IInventoryPhysicalCountService physicalCountService,
                    CancellationToken cancellationToken) =>
                {
                    var physicalCount =
                        await physicalCountService.GetByIdAsync(
                            id,
                            cancellationToken);

                    return physicalCount is null
                        ? Results.NotFound()
                        : Results.Ok(physicalCount);
                })
            .WithName("GetInventoryPhysicalCount");

        group.MapPut(
                "/physical-counts/{id:int}/lines",
                async (
                    int id,
                    PhysicalCountUpdateRequest request,
                    ClaimsPrincipal user,
                    IInventoryPhysicalCountService physicalCountService,
                    CancellationToken cancellationToken) =>
                {
                    var validationErrors =
                        ValidatePhysicalCountUpdateRequest(request);

                    if (validationErrors.Count > 0)
                    {
                        return Results.ValidationProblem(validationErrors);
                    }

                    var userId = GetUserId(user);

                    if (userId is null)
                    {
                        return CreateInvalidUserProblem();
                    }

                    try
                    {
                        var physicalCount =
                            await physicalCountService.UpdateLinesAsync(
                                id,
                                request,
                                userId,
                                cancellationToken);

                        return physicalCount is null
                            ? Results.NotFound()
                            : Results.Ok(physicalCount);
                    }
                    catch (InvalidInventoryOperationException exception)
                    {
                        return CreateInvalidOperationProblem(exception.Message);
                    }
                    catch (InvalidInventoryUserException exception)
                    {
                        return CreateInvalidUserProblem(exception.Message);
                    }
                })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("UpdateInventoryPhysicalCountLines");

        group.MapPost(
                "/physical-counts/{id:int}/complete",
                async (
                    int id,
                    ClaimsPrincipal user,
                    IInventoryPhysicalCountService physicalCountService,
                    CancellationToken cancellationToken) =>
                {
                    var userId = GetUserId(user);

                    if (userId is null)
                    {
                        return CreateInvalidUserProblem();
                    }

                    try
                    {
                        var physicalCount =
                            await physicalCountService.CompleteAsync(
                                id,
                                userId,
                                cancellationToken);

                        return physicalCount is null
                            ? Results.NotFound()
                            : Results.Ok(physicalCount);
                    }
                    catch (InventoryPhysicalCountConflictException exception)
                    {
                        return CreateConcurrencyProblem(exception.Message);
                    }
                    catch (InventoryConcurrencyException exception)
                    {
                        return CreateConcurrencyProblem(exception.Message);
                    }
                    catch (InventoryCostIntegrityException exception)
                    {
                        return CreateInvalidOperationProblem(exception.Message);
                    }
                    catch (InvalidInventoryOperationException exception)
                    {
                        return CreateInvalidOperationProblem(exception.Message);
                    }
                    catch (InvalidInventoryUserException exception)
                    {
                        return CreateInvalidUserProblem(exception.Message);
                    }
                })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("CompleteInventoryPhysicalCount");


        group.MapGet(
                "/unknown-cost-layers",
                async (
                    IInventoryCostResolutionService costResolutionService,
                    CancellationToken cancellationToken) =>
                {
                    var layers =
                        await costResolutionService.GetUnresolvedAsync(
                            cancellationToken);

                    return Results.Ok(layers);
                })
            .WithName("GetUnknownInventoryCostLayers");

        group.MapPost(
                "/cost-layers/{layerId:int}/resolve",
                async (
                    int layerId,
                    ResolveInventoryCostRequest request,
                    ClaimsPrincipal user,
                    IInventoryCostResolutionService costResolutionService,
                    CancellationToken cancellationToken) =>
                {
                    var validationErrors = ValidateRequest(request);

                    if (validationErrors.Count > 0)
                    {
                        return Results.ValidationProblem(validationErrors);
                    }

                    var userId = GetUserId(user);

                    if (userId is null)
                    {
                        return CreateInvalidUserProblem();
                    }

                    try
                    {
                        var resolved =
                            await costResolutionService.ResolveAsync(
                                layerId,
                                request,
                                userId,
                                cancellationToken);

                        return resolved is null
                            ? Results.NotFound()
                            : Results.Ok(resolved);
                    }
                    catch (InvalidInventoryOperationException exception)
                    {
                        return CreateInvalidOperationProblem(exception.Message);
                    }
                    catch (InvalidInventoryUserException exception)
                    {
                        return CreateInvalidUserProblem(exception.Message);
                    }
                })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("ResolveInventoryCostLayer");

        return endpoints;
    }

    private static string? GetUserId(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return string.IsNullOrWhiteSpace(userId)
            ? null
            : userId;
    }

    private static IResult CreateInvalidUserProblem(
        string detail =
            "The authenticated user identifier is not available.")
    {
        return Results.Problem(
            title: "Invalid authenticated user.",
            detail: detail,
            statusCode: StatusCodes.Status401Unauthorized);
    }

    private static IResult CreateInvalidOperationProblem(
        string detail)
    {
        return Results.Problem(
            title: "Invalid inventory operation.",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);
    }

    private static IResult CreateConcurrencyProblem(
        string detail)
    {
        return Results.Problem(
            title: "Inventory concurrency conflict.",
            detail: detail,
            statusCode: StatusCodes.Status409Conflict);
    }

    private static Dictionary<string, string[]>
        ValidatePhysicalCountUpdateRequest(
            PhysicalCountUpdateRequest request)
    {
        var errors = ValidateRequest(request);

        foreach (var line in request.Lines)
        {
            var lineErrors = ValidateRequest(line);

            foreach (var error in lineErrors)
            {
                var key =
                    $"Lines[{request.Lines.IndexOf(line)}].{error.Key}";

                errors[key] = error.Value;
            }
        }

        return errors;
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
