using System.ComponentModel.DataAnnotations;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Customers;
using Revestik.Shared.Customers;

namespace Revestik.Api.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/customers")
            .WithTags("Customers")
            .RequireAuthorization(PolicyNames.ManageCustomers);

        group.MapGet(
            "/",
            async (
                [AsParameters] CustomerListRequest request,
                ICustomerService customerService,
                CancellationToken cancellationToken) =>
            {
                var validationErrors = ValidateRequest(request);

                if (validationErrors.Count > 0)
                {
                    return Results.ValidationProblem(validationErrors);
                }

                var customers = await customerService.GetPageAsync(
                    request,
                    cancellationToken);

                return Results.Ok(customers);
            })
            .WithName("GetCustomers");

        group.MapGet(
            "/{id:int}",
            async (
                int id,
                ICustomerService customerService,
                CancellationToken cancellationToken) =>
            {
                var customer = await customerService.GetByIdAsync(
                    id,
                    cancellationToken);

                return customer is null
                    ? Results.NotFound()
                    : Results.Ok(customer);
            })
            .WithName("GetCustomerById");

        group.MapPost(
            "/",
            async (
                CustomerUpsertRequest request,
                ICustomerService customerService,
                CancellationToken cancellationToken) =>
            {
                var validationErrors = ValidateRequest(request);

                if (validationErrors.Count > 0)
                {
                    return Results.ValidationProblem(validationErrors);
                }

                try
                {
                    var customer = await customerService.CreateAsync(
                        request,
                        cancellationToken);

                    return Results.Created(
                        $"/api/customers/{customer.Id}",
                        customer);
                }
                catch (DuplicateCustomerIdentificationException)
                {
                    return Results.Problem(
                        title: "Identificación duplicada.",
                        detail:
                            "Ya existe un cliente registrado con esta identificación.",
                        statusCode: StatusCodes.Status409Conflict);
                }
            })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("CreateCustomer");

        group.MapPut(
            "/{id:int}",
            async (
                int id,
                CustomerUpsertRequest request,
                ICustomerService customerService,
                CancellationToken cancellationToken) =>
            {
                var validationErrors = ValidateRequest(request);

                if (validationErrors.Count > 0)
                {
                    return Results.ValidationProblem(validationErrors);
                }

                try
                {
                    var customer = await customerService.UpdateAsync(
                        id,
                        request,
                        cancellationToken);

                    return customer is null
                        ? Results.NotFound()
                        : Results.Ok(customer);
                }
                catch (DuplicateCustomerIdentificationException)
                {
                    return Results.Problem(
                        title: "Identificación duplicada.",
                        detail:
                            "Ya existe un cliente registrado con esta identificación.",
                        statusCode: StatusCodes.Status409Conflict);
                }
            })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("UpdateCustomer");

        group.MapDelete(
            "/{id:int}",
            async (
                int id,
                ICustomerService customerService,
                CancellationToken cancellationToken) =>
            {
                var wasDeactivated = await customerService.DeactivateAsync(
                    id,
                    cancellationToken);

                return wasDeactivated
                    ? Results.NoContent()
                    : Results.NotFound();
            })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("DeactivateCustomer");

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