using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Sales;
using Revestik.Api.Services.Sales.Pdf;
using Revestik.Shared.Sales;

namespace Revestik.Api.Endpoints;

public static class SaleEndpoints
{
    public static IEndpointRouteBuilder MapSaleEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/sales")
            .WithTags("Sales")
            .RequireAuthorization(PolicyNames.ManageSales);

        group.MapGet(
            "/",
            async (
                [AsParameters] SaleListRequest request,
                ISaleService saleService,
                CancellationToken cancellationToken) =>
            {
                var validationErrors = ValidateRequest(request);

                if (validationErrors.Count > 0)
                {
                    return Results.ValidationProblem(validationErrors);
                }

                var result = await saleService.GetPageAsync(
                    request,
                    cancellationToken);

                return Results.Ok(result);
            })
            .WithName("GetSales");

        group.MapGet(
            "/summary",
            async (
                ISaleService saleService,
                CancellationToken cancellationToken) =>
            {
                var result = await saleService.GetSummaryAsync(
                    cancellationToken);

                return Results.Ok(result);
            })
            .WithName("GetSalesSummary");

        group.MapGet(
            "/{id:int}",
            async (
                int id,
                ISaleService saleService,
                CancellationToken cancellationToken) =>
            {
                var sale = await saleService.GetByIdAsync(
                    id,
                    cancellationToken);

                return sale is null
                    ? Results.NotFound()
                    : Results.Ok(sale);
            })
            .WithName("GetSaleById");


        group.MapGet(
            "/{id:int}/pdf",
            async (
                int id,
                ISaleService saleService,
                ISalePdfService salePdfService,
                CancellationToken cancellationToken) =>
            {
                var sale = await saleService.GetByIdAsync(
                    id,
                    cancellationToken);

                if (sale is null)
                {
                    return Results.NotFound();
                }

                if (sale.Status == SaleStatus.Draft ||
                    string.IsNullOrWhiteSpace(
                        sale.SaleNumber))
                {
                    return Results.Problem(
                        title: "Sale PDF unavailable.",
                        detail:
                            "Only issued or voided sales have an official internal sale document.",
                        statusCode:
                            StatusCodes.Status400BadRequest);
                }

                var pdf =
                    salePdfService.Generate(sale);

                var fileName =
                    $"{sale.SaleNumber}.pdf";

                return Results.File(
                    pdf,
                    contentType:
                        "application/pdf",
                    fileDownloadName:
                        fileName);
            })
            .WithName("DownloadSalePdf");

        group.MapPost(
            "/",
            async (
                SaleUpsertRequest request,
                ClaimsPrincipal user,
                ISaleService saleService,
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
                    return InvalidAuthenticatedUser();
                }

                try
                {
                    var sale = await saleService.CreateAsync(
                        request,
                        userId,
                        cancellationToken);

                    return Results.Created(
                        $"/api/sales/{sale.Id}",
                        sale);
                }
                catch (InvalidOperationException exception)
                {
                    return MapBusinessError(exception);
                }
                catch (ArgumentException exception)
                {
                    return MapArgumentError(exception);
                }
            })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("CreateSale");

        group.MapPut(
            "/{id:int}",
            async (
                int id,
                SaleUpsertRequest request,
                ISaleService saleService,
                CancellationToken cancellationToken) =>
            {
                var validationErrors = ValidateRequest(request);

                if (validationErrors.Count > 0)
                {
                    return Results.ValidationProblem(validationErrors);
                }

                try
                {
                    var sale = await saleService.UpdateDraftAsync(
                        id,
                        request,
                        cancellationToken);

                    return sale is null
                        ? Results.NotFound()
                        : Results.Ok(sale);
                }
                catch (InvalidOperationException exception)
                {
                    return MapBusinessError(exception);
                }
                catch (ArgumentException exception)
                {
                    return MapArgumentError(exception);
                }
            })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("UpdateSale");

        group.MapPost(
            "/from-quotation/{quotationId:int}",
            async (
                int quotationId,
                ClaimsPrincipal user,
                ISaleService saleService,
                CancellationToken cancellationToken) =>
            {
                var userId = GetUserId(user);

                if (userId is null)
                {
                    return InvalidAuthenticatedUser();
                }

                try
                {
                    var sale =
                        await saleService.CreateFromQuotationAsync(
                            quotationId,
                            userId,
                            cancellationToken);

                    return sale is null
                        ? Results.NotFound()
                        : Results.Created(
                            $"/api/sales/{sale.Id}",
                            sale);
                }
                catch (InvalidOperationException exception)
                {
                    return MapBusinessError(exception);
                }
            })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("CreateSaleFromQuotation");

        group.MapPost(
            "/{id:int}/issue",
            async (
                int id,
                SaleUpsertRequest request,
                ClaimsPrincipal user,
                ISaleService saleService,
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
                    return InvalidAuthenticatedUser();
                }

                try
                {
                    var sale = await saleService.IssueAsync(
                        id,
                        request,
                        userId,
                        cancellationToken);

                    return sale is null
                        ? Results.NotFound()
                        : Results.Ok(sale);
                }
                catch (InvalidOperationException exception)
                {
                    return MapBusinessError(exception);
                }
                catch (ArgumentException exception)
                {
                    return MapArgumentError(exception);
                }
            })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("IssueSale");

        group.MapPost(
            "/{id:int}/void",
            async (
                int id,
                VoidSaleRequest request,
                ClaimsPrincipal user,
                ISaleService saleService,
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
                    return InvalidAuthenticatedUser();
                }

                try
                {
                    var sale = await saleService.VoidAsync(
                        id,
                        request,
                        userId,
                        cancellationToken);

                    return sale is null
                        ? Results.NotFound()
                        : Results.Ok(sale);
                }
                catch (InvalidOperationException exception)
                {
                    return MapBusinessError(exception);
                }
                catch (ArgumentException exception)
                {
                    return MapArgumentError(exception);
                }
            })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("VoidSale");

        group.MapPost(
            "/{id:int}/replacement",
            async (
                int id,
                VoidSaleRequest request,
                ClaimsPrincipal user,
                ISaleService saleService,
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
                    return InvalidAuthenticatedUser();
                }

                try
                {
                    var sale =
                        await saleService.CreateReplacementAsync(
                            id,
                            request,
                            userId,
                            cancellationToken);

                    return sale is null
                        ? Results.NotFound()
                        : Results.Created(
                            $"/api/sales/{sale.Id}",
                            sale);
                }
                catch (InvalidOperationException exception)
                {
                    return MapBusinessError(exception);
                }
                catch (ArgumentException exception)
                {
                    return MapArgumentError(exception);
                }
            })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("CreateSaleReplacement");

        group.MapPost(
            "/{id:int}/payments",
            async (
                int id,
                SalePaymentRequest request,
                ClaimsPrincipal user,
                ISaleService saleService,
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
                    return InvalidAuthenticatedUser();
                }

                try
                {
                    var sale =
                        await saleService.RegisterPaymentAsync(
                            id,
                            request,
                            userId,
                            cancellationToken);

                    return sale is null
                        ? Results.NotFound()
                        : Results.Ok(sale);
                }
                catch (InvalidOperationException exception)
                {
                    return MapBusinessError(exception);
                }
                catch (ArgumentException exception)
                {
                    return MapArgumentError(exception);
                }
            })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("RegisterSalePayment");

        group.MapPost(
            "/{id:int}/payments/{paymentId:int}/void",
            async (
                int id,
                int paymentId,
                VoidSalePaymentRequest request,
                ClaimsPrincipal user,
                ISaleService saleService,
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
                    return InvalidAuthenticatedUser();
                }

                try
                {
                    var sale = await saleService.VoidPaymentAsync(
                        id,
                        paymentId,
                        request,
                        userId,
                        cancellationToken);

                    return sale is null
                        ? Results.NotFound()
                        : Results.Ok(sale);
                }
                catch (InvalidOperationException exception)
                {
                    return MapBusinessError(exception);
                }
                catch (ArgumentException exception)
                {
                    return MapArgumentError(exception);
                }
            })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("VoidSalePayment");

        return endpoints;
    }

    private static string? GetUserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier);

    private static IResult InvalidAuthenticatedUser() =>
        Results.Problem(
            title: "Invalid authenticated user.",
            detail:
                "The authenticated user identifier is not available.",
            statusCode: StatusCodes.Status401Unauthorized);

    private static IResult MapBusinessError(
        InvalidOperationException exception) =>
        Results.Problem(
            title: "Sale operation rejected.",
            detail: exception.Message,
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult MapArgumentError(
        ArgumentException exception) =>
        Results.Problem(
            title: "Invalid sale request.",
            detail: exception.Message,
            statusCode: StatusCodes.Status400BadRequest);

    private static Dictionary<string, string[]> ValidateRequest<TRequest>(
        TRequest request)
        where TRequest : class
    {
        var validationResults =
            new List<ValidationResult>();

        var validationContext =
            new ValidationContext(request);

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
                            result.ErrorMessage ??
                            "Invalid value."
                    }))
            .GroupBy(error => error.MemberName)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(error => error.ErrorMessage)
                    .ToArray());
    }
}