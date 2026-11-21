using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Revestik.Api.Authorization;
using Revestik.Api.Extensions;
using Revestik.Api.Services.PublicCatalog;
using Revestik.Shared.PublicSite;

namespace Revestik.Api.Endpoints.PublicCatalog;

public static class PublicCartEndpoints
{
    public static IEndpointRouteBuilder MapPublicCartEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/public/cart")
            .RequireAuthorization(PolicyNames.PublicCustomerOnly)
            .RequireRateLimiting(RevestikRateLimiting.PublicCart)
            .WithTags("Public customer cart");

        group.MapGet("", async (ClaimsPrincipal user, PublicCartService cart,
            HttpContext context, CancellationToken ct) =>
        {
            NoStore(context);
            var owner = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (owner is null || !await cart.IsCurrentPublicCustomerAsync(owner, ct))
                return (IResult)Results.Forbid();
            return Results.Ok(await cart.GetAsync(owner, ct));
        });

        group.MapPut("/items/{productId:int}", async (
            int productId, PublicCartQuantityWrite request, ClaimsPrincipal user,
            PublicCartService cart, HttpContext context, CancellationToken ct) =>
        {
            NoStore(context);
            var owner = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (owner is null || !await cart.IsCurrentPublicCustomerAsync(owner, ct))
                return (IResult)Results.Forbid();
            var result = await cart.SetAsync(owner, productId, request, ct);
            IResult outcome = result switch
            {
                PublicCartResult.Success => Results.Ok(await cart.GetAsync(owner, ct)),
                PublicCartResult.NotFound => Results.NotFound(),
                PublicCartResult.InvalidQuantity => Results.BadRequest(new { message = "Invalid quantity for this selling unit." }),
                _ => Results.Conflict(new { message = "Cart or product changed. Reload before editing." })
            };
            return outcome;
        }).AddEndpointFilter<AntiforgeryValidationFilter>();

        group.MapDelete("/items/{productId:int}", async (
            int productId, string? version, ClaimsPrincipal user, PublicCartService cart,
            HttpContext context, CancellationToken ct) =>
        {
            NoStore(context);
            var owner = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (owner is null || !await cart.IsCurrentPublicCustomerAsync(owner, ct))
                return (IResult)Results.Forbid();
            var result = await cart.DeleteAsync(owner, productId, version, ct);
            IResult outcome = result switch
            {
                PublicCartResult.Success => Results.NoContent(),
                PublicCartResult.NotFound => Results.NotFound(),
                _ => Results.Conflict(new { message = "Cart changed. Reload before deleting." })
            };
            return outcome;
        }).AddEndpointFilter<AntiforgeryValidationFilter>();
        return app;
    }

    private static void NoStore(HttpContext context) =>
        context.Response.Headers.CacheControl = "private, no-store";
}
