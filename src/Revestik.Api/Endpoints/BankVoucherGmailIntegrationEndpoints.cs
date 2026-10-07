using System.Security.Claims;
using Revestik.Api.Authorization;
using Revestik.Api.Configuration;
using Revestik.Api.Services.GmailIntegration;
using Revestik.Shared.Integrations.Gmail;

namespace Revestik.Api.Endpoints;

public static class BankVoucherGmailIntegrationEndpoints
{
    public static IEndpointRouteBuilder MapBankVoucherGmailIntegrationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/integrations/gmail/bank-vouchers")
            .WithTags("Bank voucher Gmail integration");

        group.MapGet(
                "/status",
                async (
                    IBankVoucherGmailIntegrationService service,
                    CancellationToken cancellationToken) =>
                    Results.Ok(await service.GetStatusAsync(cancellationToken)))
            .RequireAuthorization(PolicyNames.ManageExpenses)
            .WithName("GetBankVoucherGmailIntegrationStatus");

        group.MapGet(
                "/connect-url",
                (
                    ClaimsPrincipal user,
                    IBankVoucherGmailIntegrationService service) =>
                {
                    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (string.IsNullOrWhiteSpace(userId))
                        return Results.Unauthorized();

                    try
                    {
                        return Results.Ok(
                            new GmailMailboxAuthorizationUrlResponse(
                                service.CreateAuthorizationUrl(userId)));
                    }
                    catch (GmailIntegrationException exception)
                    {
                        return Results.Problem(
                            title: "No se pudo iniciar la conexión con Gmail para vouchers.",
                            detail: exception.Message,
                            statusCode: StatusCodes.Status503ServiceUnavailable);
                    }
                })
            .RequireAuthorization(PolicyNames.AdministratorOnly)
            .WithName("GetBankVoucherGmailAuthorizationUrl");

        group.MapGet(
                "/oauth/callback",
                async (
                    string? code,
                    string? state,
                    string? error,
                    ClaimsPrincipal user,
                    IBankVoucherGmailIntegrationService service,
                    Microsoft.Extensions.Options.IOptions<BankVoucherGmailIntegrationOptions> options,
                    CancellationToken cancellationToken) =>
                {
                    var returnUrl =
                        $"{options.Value.ClientBaseUrl.TrimEnd('/')}/expenses";

                    if (!string.IsNullOrWhiteSpace(error))
                        return Results.Redirect($"{returnUrl}?bankGmail=error");

                    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (string.IsNullOrWhiteSpace(userId))
                        return Results.Redirect($"{returnUrl}?bankGmail=unauthorized");

                    try
                    {
                        await service.CompleteAuthorizationAsync(
                            code ?? string.Empty,
                            state ?? string.Empty,
                            userId,
                            cancellationToken);

                        return Results.Redirect($"{returnUrl}?bankGmail=connected");
                    }
                    catch (GmailIntegrationException)
                    {
                        return Results.Redirect($"{returnUrl}?bankGmail=error");
                    }
                })
            .RequireAuthorization(PolicyNames.AdministratorOnly)
            .WithName("CompleteBankVoucherGmailAuthorization");

        group.MapPost(
                "/disconnect",
                async (
                    IBankVoucherGmailIntegrationService service,
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
            .WithName("DisconnectBankVoucherGmailIntegration");

        return endpoints;
    }
}
