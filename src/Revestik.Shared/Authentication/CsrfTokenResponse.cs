namespace Revestik.Shared.Authentication;

public sealed record CsrfTokenResponse(
    string RequestToken);