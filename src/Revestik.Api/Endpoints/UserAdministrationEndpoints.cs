using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Revestik.Api.Authorization;
using Revestik.Api.Services.Users;
using Revestik.Shared.Users;

namespace Revestik.Api.Endpoints;

public static class UserAdministrationEndpoints
{
    public static IEndpointRouteBuilder MapUserAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/settings/users")
            .WithTags("User Administration")
            .RequireAuthorization(PolicyNames.AdministratorOnly);

        group.MapGet("/", async (UserAdministrationService service, CancellationToken ct) =>
            Results.Ok(new ManagedUserListResponse(await service.ListAsync(ct))));

        group.MapGet("/{id}", async (string id, UserAdministrationService service, CancellationToken ct) =>
        {
            var result = await service.GetAsync(id, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("/", async ([FromBody] CreateManagedUserRequest request,
            ClaimsPrincipal principal, UserAdministrationService service, CancellationToken ct) =>
        {
            var actorId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(actorId)) return Results.Unauthorized();
            try
            {
                return Results.Created("/api/settings/users", await service.CreateAsync(request, actorId, ct));
            }
            catch (UserAdministrationException ex) { return Error(ex); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>();

        group.MapPut("/{id}", async (string id, [FromBody] UpdateManagedUserRequest request,
            ClaimsPrincipal principal, UserAdministrationService service, CancellationToken ct) =>
        {
            var actorId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(actorId)) return Results.Unauthorized();
            try
            {
                return Results.Ok(await service.UpdateAsync(id, request, actorId, ct));
            }
            catch (UserAdministrationException ex) { return Error(ex); }
        }).AddEndpointFilter<AntiforgeryValidationFilter>();
        return endpoints;
    }

    private static IResult Error(UserAdministrationException ex) =>
        Results.Problem(title: ex.Message, statusCode: ex.StatusCode);
}
