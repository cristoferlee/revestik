using System.Net.Http.Json;

namespace Revestik.Api.Services.GmailIntegration;

internal sealed class GmailOAuthClient(IHttpClientFactory httpClientFactory)
{
    private const string GmailReadonlyScope =
        "https://www.googleapis.com/auth/gmail.readonly";
    private const string AuthorizationEndpoint =
        "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint =
        "https://oauth2.googleapis.com/token";
    private const string RevokeEndpoint =
        "https://oauth2.googleapis.com/revoke";
    private const string GmailApiBaseUrl =
        "https://gmail.googleapis.com/gmail/v1/";

    public string CreateAuthorizationUrl(
        string clientId,
        string redirectUri,
        string protectedState)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = GmailReadonlyScope,
            ["access_type"] = "offline",
            ["include_granted_scopes"] = "true",
            ["prompt"] = "consent",
            ["state"] = protectedState
        };

        return AddQueryString(AuthorizationEndpoint, parameters);
    }

    public async Task<GoogleTokenResponse> ExchangeAuthorizationCodeAsync(
        string code,
        string clientId,
        string clientSecret,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient();
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            });

        using var response = await client.PostAsync(
            TokenEndpoint,
            content,
            cancellationToken);

        var token = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(
            cancellationToken);

        if (!response.IsSuccessStatusCode || token is null)
        {
            throw new GmailIntegrationException(
                token?.ErrorDescription ??
                "Google rechazó el intercambio del código OAuth.");
        }

        return token;
    }

    public async Task<string> RefreshAccessTokenAsync(
        string refreshToken,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient();
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["refresh_token"] = refreshToken,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "refresh_token"
            });

        using var response = await client.PostAsync(
            TokenEndpoint,
            content,
            cancellationToken);

        var token = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(
            cancellationToken);

        if (!response.IsSuccessStatusCode ||
            token is null ||
            string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new GmailIntegrationException(
                token?.ErrorDescription ??
                "No fue posible renovar el acceso de Gmail. Vuelve a conectar la cuenta.");
        }

        return token.AccessToken;
    }

    public async Task<string> GetConnectedMailboxAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var profile = await GetAuthorizedJsonAsync<GmailProfileDto>(
            accessToken,
            $"{GmailApiBaseUrl}users/me/profile",
            cancellationToken);

        return profile.EmailAddress;
    }

    public async Task<bool> TryRevokeAsync(
        string token,
        CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient();
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["token"] = token
            });

        using var response = await client.PostAsync(
            RevokeEndpoint,
            content,
            cancellationToken);

        return response.IsSuccessStatusCode;
    }

    private async Task<T> GetAuthorizedJsonAsync<T>(
        string accessToken,
        string url,
        CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new GmailIntegrationException(
                $"Gmail API devolvió HTTP {(int)response.StatusCode}. {Truncate(body, 180)}");
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new GmailIntegrationException(
                "Gmail API devolvió una respuesta vacía.");
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength
            ? value
            : value[..maxLength];

    private static string AddQueryString(
        string baseUrl,
        IReadOnlyDictionary<string, string?> parameters)
    {
        var values = parameters
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x =>
                $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value!)}");

        return $"{baseUrl}?{string.Join("&", values)}";
    }
}
