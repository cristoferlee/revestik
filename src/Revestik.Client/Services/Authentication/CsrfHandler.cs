namespace Revestik.Client.Services.Authentication;

public sealed class CsrfHandler(
    CsrfTokenService csrfTokenService,
    Uri apiBaseAddress)
    : DelegatingHandler
{
    private const string HeaderName = "X-CSRF-TOKEN";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!RequiresCsrfProtection(request.Method) ||
            !IsApiRequest(request.RequestUri))
        {
            return await base.SendAsync(
                request,
                cancellationToken);
        }

        var requestToken =
            await csrfTokenService.GetTokenAsync(
                cancellationToken);

        request.Headers.Remove(HeaderName);
        request.Headers.TryAddWithoutValidation(
            HeaderName,
            requestToken);

        return await base.SendAsync(
            request,
            cancellationToken);
    }

    private static bool RequiresCsrfProtection(
        HttpMethod method)
    {
        return method == HttpMethod.Post ||
               method == HttpMethod.Put ||
               method == HttpMethod.Patch ||
               method == HttpMethod.Delete;
    }

    private bool IsApiRequest(Uri? requestUri)
    {
        if (requestUri is null)
        {
            return false;
        }

        var absoluteRequestUri = requestUri.IsAbsoluteUri
            ? requestUri
            : new Uri(apiBaseAddress, requestUri);

        return string.Equals(
                   absoluteRequestUri.Scheme,
                   apiBaseAddress.Scheme,
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   absoluteRequestUri.Host,
                   apiBaseAddress.Host,
                   StringComparison.OrdinalIgnoreCase) &&
               absoluteRequestUri.Port ==
                   apiBaseAddress.Port;
    }
}