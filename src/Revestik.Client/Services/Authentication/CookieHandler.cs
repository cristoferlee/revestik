using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Revestik.Client.Services.Authentication;

public sealed class CookieHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // The API uses an HttpOnly authentication cookie.
        // Browser credentials must be included in requests
        // sent from the Blazor client to the API.
        request.SetBrowserRequestCredentials(
            BrowserRequestCredentials.Include);

        return base.SendAsync(request, cancellationToken);
    }
}