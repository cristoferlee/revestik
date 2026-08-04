namespace Revestik.Shared.Authentication;

public sealed record AuthenticationProviderResponse(
    string Id,
    string DisplayName,
    bool IsAvailable);