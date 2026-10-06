using System.Security.Claims;
using Revestik.Api.Authorization;
using Revestik.Api.Configuration;
using Revestik.Api.Services.GmailIntegration;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Endpoints;

public static class GmailIntegrationEndpoints
{
    public static IEndpointRouteBuilder MapGmailIntegrationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/integrations/gmail")
            .WithTags("Gmail integration");

        group.MapGet(
                "/status",
                async (
                    IGmailIntegrationService service,
                    CancellationToken cancellationToken) =>
                    Results.Ok(await service.GetStatusAsync(cancellationToken)))
            .RequireAuthorization(PolicyNames.ManagePurchases)
            .WithName("GetGmailIntegrationStatus");

        group.MapGet(
                "/connect-url",
                (
                    ClaimsPrincipal user,
                    IGmailIntegrationService service) =>
                {
                    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (string.IsNullOrWhiteSpace(userId))
                        return Results.Unauthorized();

                    try
                    {
                        return Results.Ok(
                            new GmailAuthorizationUrlResponse(
                                service.CreateAuthorizationUrl(userId)));
                    }
                    catch (GmailIntegrationException exception)
                    {
                        return Results.Problem(
                            title: "No se pudo iniciar la conexión con Gmail.",
                            detail: exception.Message,
                            statusCode: StatusCodes.Status503ServiceUnavailable);
                    }
                })
            .RequireAuthorization(PolicyNames.AdministratorOnly)
            .WithName("GetGmailAuthorizationUrl");

        group.MapGet(
                "/oauth/callback",
                async (
                    string? code,
                    string? state,
                    string? error,
                    ClaimsPrincipal user,
                    IGmailIntegrationService service,
                    Microsoft.Extensions.Options.IOptions<GmailIntegrationOptions> options,
                    CancellationToken cancellationToken) =>
                {
                    var returnUrl =
                        $"{options.Value.ClientBaseUrl.TrimEnd('/')}/purchases/received-documents";

                    if (!string.IsNullOrWhiteSpace(error))
                        return Results.Redirect($"{returnUrl}?gmail=error");

                    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (string.IsNullOrWhiteSpace(userId))
                        return Results.Redirect($"{returnUrl}?gmail=unauthorized");

                    try
                    {
                        await service.CompleteAuthorizationAsync(
                            code ?? string.Empty,
                            state ?? string.Empty,
                            userId,
                            cancellationToken);

                        return Results.Redirect($"{returnUrl}?gmail=connected");
                    }
                    catch (GmailIntegrationException)
                    {
                        return Results.Redirect($"{returnUrl}?gmail=error");
                    }
                })
            .RequireAuthorization(PolicyNames.AdministratorOnly)
            .WithName("CompleteGmailAuthorization");

        group.MapPost(
                "/sync",
                async (
                    IGmailIntegrationService service,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        return Results.Ok(await service.SyncAsync(cancellationToken));
                    }
                    catch (GmailSyncAlreadyRunningException exception)
                    {
                        return Results.Problem(
                            title: "Sincronización en curso.",
                            detail: exception.Message,
                            statusCode: StatusCodes.Status409Conflict);
                    }
                    catch (GmailIntegrationException exception)
                    {
                        return Results.Problem(
                            title: "No se pudo sincronizar Gmail.",
                            detail: exception.Message,
                            statusCode: StatusCodes.Status400BadRequest);
                    }
                })
            .RequireAuthorization(PolicyNames.ManageAccountingClassification)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("SyncGmailReceivedDocuments");

        group.MapPost(
                "/disconnect",
                async (
                    IGmailIntegrationService service,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        await service.DisconnectAsync(cancellationToken);
                        return Results.NoContent();
                    }
                    catch (GmailSyncAlreadyRunningException exception)
                    {
                        return Results.Problem(
                            title: "Sincronización en curso.",
                            detail: exception.Message,
                            statusCode: StatusCodes.Status409Conflict);
                    }
                })
            .RequireAuthorization(PolicyNames.AdministratorOnly)
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithName("DisconnectGmailIntegration");

        return endpoints;
    }
}
