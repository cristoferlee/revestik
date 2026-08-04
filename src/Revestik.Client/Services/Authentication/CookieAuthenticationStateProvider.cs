using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Revestik.Shared.Authentication;

namespace Revestik.Client.Services.Authentication;

public sealed class CookieAuthenticationStateProvider(
    HttpClient httpClient)
    : AuthenticationStateProvider
{
    private static readonly AuthenticationState AnonymousState =
        new(new ClaimsPrincipal(
            new ClaimsIdentity()));

    public override async Task<AuthenticationState>
        GetAuthenticationStateAsync()
    {
        try
        {
            var response = await httpClient.GetAsync("api/auth/me");

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return AnonymousState;
            }

            response.EnsureSuccessStatusCode();

            var currentUser = await response.Content
                .ReadFromJsonAsync<CurrentUserResponse>();

            if (currentUser is null)
            {
                return AnonymousState;
            }

            var claims = new List<Claim>
            {
                new(
                    ClaimTypes.NameIdentifier,
                    currentUser.Id),
                new(
                    ClaimTypes.Name,
                    currentUser.DisplayName),
                new(
                    ClaimTypes.Email,
                    currentUser.Email)
            };

            claims.AddRange(
                currentUser.Roles.Select(role =>
                    new Claim(ClaimTypes.Role, role)));

            var identity = new ClaimsIdentity(
                claims,
                authenticationType: "RevestikCookie",
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role);

            return new AuthenticationState(
                new ClaimsPrincipal(identity));
        }
        catch (HttpRequestException)
        {
            return AnonymousState;
        }
    }

    public void RefreshAuthenticationState()
    {
        NotifyAuthenticationStateChanged(
            GetAuthenticationStateAsync());
    }
}