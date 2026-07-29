using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Revestik.Shared.Authentication;

namespace Revestik.Client.Services.Authentication;

public sealed class AuthenticationService(
    HttpClient httpClient,
    NavigationManager navigationManager,
    CookieAuthenticationStateProvider authenticationStateProvider)
    : IAuthenticationService
{
    public async Task<AuthenticationProviderResponse> GetProviderAsync(
        CancellationToken cancellationToken = default)
    {
        return await httpClient
            .GetFromJsonAsync<AuthenticationProviderResponse>(
                "api/auth/providers",
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The API returned an empty authentication provider response.");
    }

    public void LoginWithGoogle(string? returnPath = null)
    {
        var safeReturnPath = NormalizeReturnPath(returnPath);
        var encodedReturnPath =
            Uri.EscapeDataString(safeReturnPath);

        var loginUri = new Uri(
            httpClient.BaseAddress
                ?? throw new InvalidOperationException(
                    "The API base URL was not configured."),
            $"api/auth/login/google?returnPath={encodedReturnPath}");

        navigationManager.NavigateTo(
            loginUri.AbsoluteUri,
            forceLoad: true);
    }

    public async Task LogoutAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            "api/auth/logout",
            new LogoutRequest(Confirm: true),
            cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            response.EnsureSuccessStatusCode();
        }

        authenticationStateProvider.RefreshAuthenticationState();
        navigationManager.NavigateTo(
            "/login",
            forceLoad: false);
    }

    private static string NormalizeReturnPath(string? returnPath)
    {
        if (string.IsNullOrWhiteSpace(returnPath) ||
            !returnPath.StartsWith('/') ||
            returnPath.StartsWith("//"))
        {
            return "/";
        }

        return returnPath;
    }
}
