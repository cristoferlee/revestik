using System.ComponentModel.DataAnnotations;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Suppliers;
using Revestik.Shared.Suppliers;

namespace Revestik.Api.Endpoints;

public static class SupplierEndpoints
{
    public static IEndpointRouteBuilder MapSupplierEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/suppliers")
            .WithTags("Suppliers")
            .RequireAuthorization(PolicyNames.ManagePurchases);

        group.MapGet(
            "/",
            async (
                [AsParameters] SupplierListRequest request,
                ISupplierService supplierService,
                CancellationToken cancellationToken) =>
            {
                var validationErrors = ValidateRequest(request);

                if (validationErrors.Count > 0)
                {
                    return Results.ValidationProblem(validationErrors);
                }

                var suppliers = await supplierService.GetPageAsync(
                    request,
                    cancellationToken);

                return Results.Ok(suppliers);
            })
            .WithName("GetSuppliers");

        group.MapGet(
            "/{id:int}",
            async (
                int id,
                ISupplierService supplierService,
                CancellationToken cancellationToken) =>
            {
                var supplier = await supplierService.GetByIdAsync(
                    id,
                    cancellationToken);

                return supplier is null
                    ? Results.NotFound()
                    : Results.Ok(supplier);
            })
            .WithName("GetSupplierById");

        group.MapPost(
            "/",
            async (
                SupplierUpsertRequest request,
                ISupplierService supplierService,
                CancellationToken cancellationToken) =>
            {
                var validationErrors = ValidateRequest(request);

                if (validationErrors.Count > 0)
                {
                    return Results.ValidationProblem(validationErrors);
                }

                try
                {
                    var supplier = await supplierService.CreateAsync(
                        request,
                        cancellationToken);

                    return Results.Created(
                        $"/api/suppliers/{supplier.Id}",
                        supplier);
                }
                catch (DuplicateSupplierNameException)
                {
                    return DuplicateName();
                }
            })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("CreateSupplier");

        group.MapPut(
            "/{id:int}",
            async (
                int id,
                SupplierUpsertRequest request,
                ISupplierService supplierService,
                CancellationToken cancellationToken) =>
            {
                var validationErrors = ValidateRequest(request);

                if (validationErrors.Count > 0)
                {
                    return Results.ValidationProblem(validationErrors);
                }

                try
                {
                    var supplier = await supplierService.UpdateAsync(
                        id,
                        request,
                        cancellationToken);

                    return supplier is null
                        ? Results.NotFound()
                        : Results.Ok(supplier);
                }
                catch (DuplicateSupplierNameException)
                {
                    return DuplicateName();
                }
            })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("UpdateSupplier");

        group.MapPost(
            "/{id:int}/deactivate",
            async (
                int id,
                ISupplierService supplierService,
                CancellationToken cancellationToken) =>
            {
                var updated = await supplierService.DeactivateAsync(
                    id,
                    cancellationToken);

                return updated
                    ? Results.NoContent()
                    : Results.NotFound();
            })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("DeactivateSupplier");

        group.MapPost(
            "/{id:int}/reactivate",
            async (
                int id,
                ISupplierService supplierService,
                CancellationToken cancellationToken) =>
            {
                var updated = await supplierService.ReactivateAsync(
                    id,
                    cancellationToken);

                return updated
                    ? Results.NoContent()
                    : Results.NotFound();
            })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("ReactivateSupplier");

        return endpoints;
    }

    private static IResult DuplicateName() => Results.Problem(
        title: "Proveedor duplicado.",
        detail: "Ya existe un proveedor registrado con este nombre comercial.",
        statusCode: StatusCodes.Status409Conflict);

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