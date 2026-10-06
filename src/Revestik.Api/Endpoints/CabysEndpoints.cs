using System.ComponentModel.DataAnnotations;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Cabys;
using Revestik.Shared.Cabys;

namespace Revestik.Api.Endpoints;

public static class CabysEndpoints
{
    public static IEndpointRouteBuilder MapCabysEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/cabys")
            .WithTags("CABYS")
            .RequireAuthorization();

        group.MapGet(
                "/status",
                async (
                    ICabysCatalogService service,
                    CancellationToken cancellationToken) =>
                    Results.Ok(await service.GetStatusAsync(cancellationToken)))
            .WithName("GetCabysCatalogStatus");

        group.MapGet(
                "/",
                async (
                    [AsParameters] CabysSearchRequest request,
                    ICabysCatalogService service,
                    CancellationToken cancellationToken) =>
                {
                    var errors = ValidateRequest(request);
                    return errors.Count > 0
                        ? Results.ValidationProblem(errors)
                        : Results.Ok(await service.SearchAsync(request, cancellationToken));
                })
            .WithName("SearchCabysCatalog");

        group.MapGet(
                "/{code}",
                async (
                    string code,
                    ICabysCatalogService service,
                    CancellationToken cancellationToken) =>
                {
                    if (code.Length != 13 || !code.All(char.IsDigit))
                    {
                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                ["code"] = ["El código CAByS debe contener exactamente 13 dígitos."]
                            });
                    }

                    var item = await service.GetByCodeAsync(code, cancellationToken);
                    return item is null ? Results.NotFound() : Results.Ok(item);
                })
            .WithName("GetCabysByCode");

        group.MapPost(
                "/catalog/load-bundled",
                async (
                    ICabysCatalogService service,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        return Results.Ok(
                            await service.LoadBundledCatalogAsync(cancellationToken));
                    }
                    catch (InvalidOperationException exception)
                    {
                        return Results.Problem(
                            title: "No fue posible cargar el catálogo CAByS.",
                            detail: exception.Message,
                            statusCode: StatusCodes.Status409Conflict);
                    }
                })
            .RequireAuthorization(PolicyNames.AdministratorOnly)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("LoadBundledCabysCatalog");

        return endpoints;
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
                        ErrorMessage = result.ErrorMessage ?? "Valor inválido."
                    }))
            .GroupBy(error => error.MemberName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());
    }
}
