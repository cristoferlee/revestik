using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Revestik.Api.Authorization;

namespace Revestik.Api.Tests.Hosting;

public sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        options,
        logger,
        encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeaderName = "X-Test-User";
    public const string RoleHeaderName = "X-Test-Role";

    protected override Task<AuthenticateResult>
        HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(
                UserHeaderName,
                out var userName) ||
            string.IsNullOrWhiteSpace(userName))
        {
            return Task.FromResult(
                AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                userName.ToString()),
            new Claim(
                ClaimTypes.Name,
                userName.ToString())
        };

        if (!Request.Headers.TryGetValue(
                RoleHeaderName,
                out var roleValues))
        {
            claims.Add(
                new Claim(
                    ClaimTypes.Role,
                    RoleNames.Administrator));
        }
        else if (!string.IsNullOrWhiteSpace(roleValues))
        {
            claims.Add(
                new Claim(
                    ClaimTypes.Role,
                    roleValues.ToString()));
        }

        var identity = new ClaimsIdentity(
            claims,
            SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(
            principal,
            SchemeName);

        return Task.FromResult(
            AuthenticateResult.Success(ticket));
    }
}
