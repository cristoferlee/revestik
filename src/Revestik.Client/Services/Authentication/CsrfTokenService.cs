using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Revestik.Shared.Authentication;

namespace Revestik.Client.Services.Authentication;

public sealed class CsrfTokenService(
    [FromKeyedServices("CsrfTokenClient")]
    HttpClient httpClient)
{
    public const string HttpClientKey = "CsrfTokenClient";

    private string? requestToken;

    public async Task<string> GetTokenAsync(
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(requestToken))
        {
            return requestToken;
        }

        var response = await httpClient
            .GetFromJsonAsync<CsrfTokenResponse>(
                "api/auth/csrf",
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The API returned an empty CSRF token response.");

        if (string.IsNullOrWhiteSpace(response.RequestToken))
        {
            throw new InvalidOperationException(
                "The API returned an empty CSRF request token.");
        }

        requestToken = response.RequestToken;

        return requestToken;
    }

    public void Clear()
    {
        requestToken = null;
    }
}