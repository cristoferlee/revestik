namespace Revestik.Shared.Authentication;

public sealed record CurrentUserResponse(
    string Id,
    string DisplayName,
    string Email,
    IReadOnlyList<string> Roles);