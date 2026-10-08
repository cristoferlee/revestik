using System.ComponentModel.DataAnnotations;
using Revestik.Api.Authorization;
using Revestik.Api.Services.BankVouchers;
using Revestik.Shared.Expenses;

namespace Revestik.Api.Endpoints;

public static class BankVoucherEndpoints
{
    public static IEndpointRouteBuilder MapBankVoucherEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/bank-vouchers")
            .WithTags("Bank vouchers")
            .RequireAuthorization(PolicyNames.ManageExpenses);

        group.MapGet(
                "/",
                async (
                    [AsParameters] BankVoucherListRequest request,
                    IBankVoucherReviewService service,
                    CancellationToken cancellationToken) =>
                {
                    var errors = ValidateRequest(request);

                    if (errors.Count > 0)
                        return Results.ValidationProblem(errors);

                    return Results.Ok(
                        await service.GetAsync(
                            request,
                            cancellationToken));
                })
            .WithName("GetBankVouchers");

        group.MapGet(
                "/{id:int}",
                async (
                    int id,
                    IBankVoucherReviewService service,
                    CancellationToken cancellationToken) =>
                {
                    var result = await service.GetByIdAsync(
                        id,
                        cancellationToken);

                    return result is null
                        ? Results.NotFound()
                        : Results.Ok(result);
                })
            .WithName("GetBankVoucher");

        group.MapPost(
                "/{id:int}/accept",
                async (
                    int id,
                    IBankVoucherReviewService service,
                    CancellationToken cancellationToken) =>
                    await service.AcceptAsync(id, cancellationToken)
                        ? Results.NoContent()
                        : Results.NotFound())
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("AcceptBankVoucher");

        group.MapPost(
                "/{id:int}/ignore",
                async (
                    int id,
                    IBankVoucherReviewService service,
                    CancellationToken cancellationToken) =>
                    await service.IgnoreAsync(id, cancellationToken)
                        ? Results.NoContent()
                        : Results.NotFound())
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("IgnoreBankVoucher");

        group.MapPost(
                "/{id:int}/match",
                async (
                    int id,
                    BankVoucherMatchRequest request,
                    IBankVoucherReviewService service,
                    CancellationToken cancellationToken) =>
                {
                    var errors = ValidateRequest(request);

                    if (errors.Count > 0)
                        return Results.ValidationProblem(errors);

                    var matched = await service.MatchAsync(
                        id,
                        request.ElectronicDocumentId,
                        cancellationToken);

                    return matched
                        ? Results.NoContent()
                        : Results.Problem(
                            title: "No se pudo vincular el voucher.",
                            detail:
                                "La factura seleccionada no existe o no cumple los criterios de monto, moneda, fecha y comercio.",
                            statusCode: StatusCodes.Status409Conflict);
                })
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("MatchBankVoucher");

        return endpoints;
    }

    private static Dictionary<string, string[]> ValidateRequest<T>(
        T request)
        where T : class
    {
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        if (request is IValidatableObject validatable)
        {
            results.AddRange(
                validatable.Validate(
                    new ValidationContext(request)));
        }

        return results
            .SelectMany(result =>
                result.MemberNames
                    .DefaultIfEmpty("request")
                    .Select(name => new
                    {
                        Name = name,
                        Error =
                            result.ErrorMessage ?? "Invalid value."
                    }))
            .GroupBy(x => x.Name)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(x => x.Error)
                    .Distinct()
                    .ToArray());
    }
}
