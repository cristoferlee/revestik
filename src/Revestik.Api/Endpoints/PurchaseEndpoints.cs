using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Services.Inventory;
using Revestik.Api.Services.Purchases;
using Revestik.Api.Services.Products;
using Revestik.Shared.Purchases;
using Revestik.Shared.Products;

namespace Revestik.Api.Endpoints;

public static class PurchaseEndpoints
{
    public static IEndpointRouteBuilder MapPurchaseEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/purchases")
            .WithTags("Purchases")
            .RequireAuthorization(PolicyNames.ManagePurchases);

        group.MapGet(
                "/",
                async (
                    [AsParameters] PurchaseListRequest request,
                    IPurchaseQueryService purchaseQueryService,
                    CancellationToken cancellationToken) =>
                {
                    var validationErrors =
                        ValidateRequest(request);

                    if (validationErrors.Count > 0)
                    {
                        return Results.ValidationProblem(
                            validationErrors);
                    }

                    var purchases =
                        await purchaseQueryService.GetPageAsync(
                            request,
                            cancellationToken);

                    return Results.Ok(purchases);
                })
            .WithName("GetPurchases");

        group.MapGet(
                "/ap-summary",
                async (
                    IPurchaseQueryService purchaseQueryService,
                    CancellationToken cancellationToken) =>
                {
                    var summary =
                        await purchaseQueryService
                            .GetApSummaryAsync(
                                cancellationToken);

                    return Results.Ok(summary);
                })
            .WithName("GetPurchaseApSummary");

        group.MapGet(
                "/alerts",
                async (
                    [AsParameters] PurchaseAlertRequest request,
                    IPurchaseQueryService purchaseQueryService,
                    CancellationToken cancellationToken) =>
                {
                    var validationErrors =
                        ValidateRequest(request);

                    if (validationErrors.Count > 0)
                    {
                        return Results.ValidationProblem(
                            validationErrors);
                    }

                    var alerts =
                        await purchaseQueryService
                            .GetDueAlertsAsync(
                                request,
                                cancellationToken);

                    return Results.Ok(alerts);
                })
            .WithName("GetPurchaseDueAlerts");

        group.MapGet(
                "/suppliers/{supplierId:int}/history",
                async (
                    int supplierId,
                    [AsParameters]
                    SupplierPurchaseHistoryRequest request,
                    IPurchaseQueryService purchaseQueryService,
                    CancellationToken cancellationToken) =>
                {
                    var validationErrors =
                        ValidateRequest(request);

                    if (validationErrors.Count > 0)
                    {
                        return Results.ValidationProblem(
                            validationErrors);
                    }

                    var history =
                        await purchaseQueryService
                            .GetSupplierHistoryAsync(
                                supplierId,
                                request,
                                cancellationToken);

                    return history is null
                        ? Results.NotFound()
                        : Results.Ok(history);
                })
            .WithName("GetSupplierPurchaseHistory");

        group.MapGet(
                "/{id:int}",
                async (
                    int id,
                    IPurchaseService purchaseService,
                    CancellationToken cancellationToken) =>
                {
                    var purchase =
                        await purchaseService.GetByIdAsync(
                            id,
                            cancellationToken);

                    return purchase is null
                        ? Results.NotFound()
                        : Results.Ok(purchase);
                })
            .WithName("GetPurchaseById");

        group.MapPost(
                "/",
                async (
                    PurchaseCreateRequest request,
                    ClaimsPrincipal user,
                    IPurchaseService purchaseService,
                    IProductService productService,
                    IAuthorizationService authorizationService,
                    IInventoryPurchaseReceiptService
                        inventoryPurchaseReceiptService,
                    RevestikDbContext dbContext,
                    CancellationToken cancellationToken) =>
                {
                    var validationErrors =
                        ValidateRequest(request);

                    if (validationErrors.Count > 0)
                    {
                        return Results.ValidationProblem(
                            validationErrors);
                    }

                    var userId = GetUserId(user);

                    if (userId is null)
                    {
                        return CreateInvalidUserProblem();
                    }

                    if (request.Lines.Any(line => line.NewProduct is not null))
                    {
                        var canManageCatalog = await authorizationService.AuthorizeAsync(
                            user,
                            PolicyNames.ManageProductCatalog);

                        if (!canManageCatalog.Succeeded)
                        {
                            return Results.Forbid();
                        }
                    }

                    await using var transaction =
                        await dbContext.Database
                            .BeginTransactionAsync(
                                cancellationToken);

                    try
                    {
                        foreach (var line in request.Lines.Where(line => line.NewProduct is not null))
                        {
                            var newProduct = line.NewProduct!;
                            var currentCost = GetInventoryUnitCost(request, line.UnitCost);

                            var createdProduct = await productService.CreateAsync(
                                new ProductUpsertRequest
                                {
                                    CategoryId = newProduct.CategoryId,
                                    Name = newProduct.Name,
                                    Description = newProduct.Description,
                                    CabysCode = newProduct.CabysCode,
                                    InventoryUnitId = newProduct.InventoryUnitId,
                                    CommercialUnitId = newProduct.CommercialUnitId,
                                    CommercialUnitsPerInventoryUnit = newProduct.CommercialUnitsPerInventoryUnit,
                                    RequiresWholeInventoryUnits = newProduct.RequiresWholeInventoryUnits,
                                    SalePrice = newProduct.SalePrice,
                                    SalePriceBasis = newProduct.SalePriceBasis,
                                    CurrentCost = currentCost,
                                    TaxRate = newProduct.TaxRate,
                                    MinimumStock = newProduct.MinimumStock
                                },
                                cancellationToken);

                            line.ProductId = createdProduct.Id;
                            line.NewProduct = null;
                        }

                        var purchase =
                            await purchaseService.CreateAsync(
                                request,
                                userId,
                                cancellationToken);

                        foreach (var line in purchase.Lines)
                        {
                            var inventoryUnitCost =
                                GetInventoryUnitCost(
                                    purchase,
                                    line);

                            await inventoryPurchaseReceiptService
                                .ReceiveAsync(
                                    line.Id,
                                    inventoryUnitCost,
                                    userId,
                                    cancellationToken);
                        }

                        await transaction.CommitAsync(
                            cancellationToken);

                        var result =
                            await purchaseService.GetByIdAsync(
                                purchase.Id,
                                cancellationToken)
                            ?? throw new InvalidOperationException(
                                "The purchase could not be loaded after commit.");

                        return Results.Created(
                            $"/api/purchases/{purchase.Id}",
                            result);
                    }
                    catch (InvalidProductCatalogReferenceException exception)
                    {
                        await transaction.RollbackAsync(cancellationToken);

                        return Results.Problem(
                            title: "Referencia de catálogo inválida.",
                            detail: exception.Message,
                            statusCode: StatusCodes.Status400BadRequest);
                    }
                    catch (InvalidPurchaseOperationException exception)
                    {
                        await transaction.RollbackAsync(
                            cancellationToken);

                        return CreatePurchaseProblem(
                            exception.Message);
                    }
                    catch (PurchaseInventoryAlreadyAppliedException exception)
                    {
                        await transaction.RollbackAsync(
                            cancellationToken);

                        return Results.Problem(
                            title: "Purchase inventory already applied.",
                            detail: exception.Message,
                            statusCode:
                                StatusCodes.Status409Conflict);
                    }
                    catch (InventoryConcurrencyException exception)
                    {
                        await transaction.RollbackAsync(
                            cancellationToken);

                        return Results.Problem(
                            title: "Inventory concurrency conflict.",
                            detail: exception.Message,
                            statusCode:
                                StatusCodes.Status409Conflict);
                    }
                    catch (InventoryCostIntegrityException exception)
                    {
                        await transaction.RollbackAsync(
                            cancellationToken);

                        return Results.Problem(
                            title: "Inventory cost integrity conflict.",
                            detail: exception.Message,
                            statusCode:
                                StatusCodes.Status409Conflict);
                    }
                    catch (InvalidInventoryOperationException exception)
                    {
                        await transaction.RollbackAsync(
                            cancellationToken);

                        return Results.Problem(
                            title: "Invalid inventory operation.",
                            detail: exception.Message,
                            statusCode:
                                StatusCodes.Status400BadRequest);
                    }
                    catch (InvalidInventoryUserException exception)
                    {
                        await transaction.RollbackAsync(
                            cancellationToken);

                        return CreateInvalidUserProblem(
                            exception.Message);
                    }
                })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("CreatePurchase");

        group.MapPost(
                "/{id:int}/payments",
                async (
                    int id,
                    PurchasePaymentRequest request,
                    ClaimsPrincipal user,
                    IPurchaseService purchaseService,
                    RevestikDbContext dbContext,
                    CancellationToken cancellationToken) =>
                {
                    var validationErrors =
                        ValidateRequest(request);

                    if (validationErrors.Count > 0)
                    {
                        return Results.ValidationProblem(
                            validationErrors);
                    }

                    var userId = GetUserId(user);

                    if (userId is null)
                    {
                        return CreateInvalidUserProblem();
                    }

                    await using var transaction =
                        await dbContext.Database
                            .BeginTransactionAsync(
                                IsolationLevel.Serializable,
                                cancellationToken);

                    try
                    {
                        var purchase =
                            await purchaseService
                                .RegisterPaymentAsync(
                                    id,
                                    request,
                                    userId,
                                    cancellationToken);

                        if (purchase is null)
                        {
                            await transaction.RollbackAsync(
                                cancellationToken);

                            return Results.NotFound();
                        }

                        await transaction.CommitAsync(
                            cancellationToken);

                        return Results.Ok(purchase);
                    }
                    catch (InvalidPurchaseOperationException exception)
                    {
                        await transaction.RollbackAsync(
                            cancellationToken);

                        return CreatePurchaseProblem(
                            exception.Message);
                    }
                })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("RegisterPurchasePayment");

        group.MapPost(
                "/{id:int}/payments/{paymentId:int}/void",
                async (
                    int id,
                    int paymentId,
                    VoidPurchasePaymentRequest request,
                    ClaimsPrincipal user,
                    IPurchaseService purchaseService,
                    CancellationToken cancellationToken) =>
                {
                    var validationErrors =
                        ValidateRequest(request);

                    if (validationErrors.Count > 0)
                    {
                        return Results.ValidationProblem(
                            validationErrors);
                    }

                    var userId = GetUserId(user);

                    if (userId is null)
                    {
                        return CreateInvalidUserProblem();
                    }

                    try
                    {
                        var purchase =
                            await purchaseService
                                .VoidPaymentAsync(
                                    id,
                                    paymentId,
                                    request,
                                    userId,
                                    cancellationToken);

                        return purchase is null
                            ? Results.NotFound()
                            : Results.Ok(purchase);
                    }
                    catch (PurchasePaymentConcurrencyException exception)
                    {
                        return Results.Problem(
                            title: "Purchase payment concurrency conflict.",
                            detail: exception.Message,
                            statusCode:
                                StatusCodes.Status409Conflict);
                    }
                    catch (InvalidPurchaseOperationException exception)
                    {
                        return CreatePurchaseProblem(
                            exception.Message);
                    }
                })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("VoidPurchasePayment");

        return endpoints;
    }

    private static decimal GetInventoryUnitCost(
        PurchaseCreateRequest purchase,
        decimal unitCost)
    {
        if (purchase.Currency == PurchaseCurrency.CRC)
        {
            return unitCost;
        }

        var exchangeRate = purchase.ExchangeRate
            ?? throw new InvalidPurchaseOperationException(
                "USD purchases require an exchange rate.");

        return decimal.Round(
            unitCost * exchangeRate,
            2,
            MidpointRounding.AwayFromZero);
    }

    private static decimal GetInventoryUnitCost(
        PurchaseResponse purchase,
        PurchaseLineResponse line)
    {
        if (purchase.Currency == PurchaseCurrency.CRC)
        {
            return line.UnitCost;
        }

        var exchangeRate = purchase.ExchangeRate
            ?? throw new InvalidPurchaseOperationException(
                "USD purchases require an exchange rate.");

        return decimal.Round(
            line.UnitCost * exchangeRate,
            2,
            MidpointRounding.AwayFromZero);
    }

    private static string? GetUserId(
        ClaimsPrincipal user)
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
            statusCode:
                StatusCodes.Status401Unauthorized);
    }

    private static IResult CreatePurchaseProblem(
        string detail)
    {
        return Results.Problem(
            title: "Purchase operation rejected.",
            detail: detail,
            statusCode:
                StatusCodes.Status400BadRequest);
    }

    private static Dictionary<string, string[]>
        ValidateRequest<TRequest>(
            TRequest request)
        where TRequest : class
    {
        var validationResults =
            new List<ValidationResult>();

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
                            result.ErrorMessage ??
                            "Invalid value."
                    }))
            .GroupBy(error => error.MemberName)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(error =>
                        error.ErrorMessage)
                    .ToArray());
    }
}