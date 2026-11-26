using System.Security.Claims;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Users;
using Revestik.Shared.Users;

namespace Revestik.Api.Endpoints;

public static class PublicCustomerAdministrationEndpoints
{
    public static IEndpointRouteBuilder MapPublicCustomerAdministrationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/settings/public-customers")
            .WithTags("Public Customer Administration")
            .RequireAuthorization(PolicyNames.AdministratorOnly);

        group.MapGet("/", async (
            string? search,
            int? page,
            int? pageSize,
            HttpContext context,
            PublicCustomerAdministrationService service,
            CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "private, no-store";

            try
            {
                return Results.Ok(await service.ListAsync(
                    search, page ?? 1, pageSize ?? 25, ct));
            }
            catch (UserAdministrationException exception)
            {
                return Results.Problem(
                    title: exception.Message,
                    statusCode: exception.StatusCode);
            }
        })
        .WithName("ListPublicCustomerAccounts");

        group.MapPut("/{id}/status", async (
            string id,
            UpdatePublicCustomerStatusRequest request,
            HttpContext context,
            PublicCustomerAdministrationService service,
            CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "private, no-store";

            var actorId = context.User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(actorId))
                return Results.Unauthorized();

            try
            {
                return Results.Ok(await service.UpdateStatusAsync(
                    id, request, actorId, ct));
            }
            catch (UserAdministrationException exception)
            {
                return Results.Problem(
                    title: exception.Message,
                    statusCode: exception.StatusCode);
            }
        })
        .AddEndpointFilter<AntiforgeryValidationFilter>()
        .WithName("UpdatePublicCustomerAccountStatus");

        return endpoints;
    }
}
