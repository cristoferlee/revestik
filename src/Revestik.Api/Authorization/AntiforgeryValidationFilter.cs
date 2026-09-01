using Microsoft.AspNetCore.Antiforgery;

namespace Revestik.Api.Authorization;

public sealed class AntiforgeryValidationFilter(
    IAntiforgery antiforgery,
    ILogger<AntiforgeryValidationFilter> logger)
    : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(
                context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            logger.LogWarning(
                "CSRF validation failed for {Method} {Path}.",
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path);

            return Results.Problem(
                title: "Invalid CSRF token.",
                statusCode:
                    StatusCodes.Status400BadRequest);
        }

        return await next(context);
    }
}