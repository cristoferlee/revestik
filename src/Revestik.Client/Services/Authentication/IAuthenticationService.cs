using Revestik.Shared.Authentication;

namespace Revestik.Client.Services.Authentication;

public interface IAuthenticationService
{
    Task<AuthenticationProviderResponse> GetProviderAsync(
        CancellationToken cancellationToken = default);

    void LoginWithGoogle(string? returnPath = null);

    Task LogoutAsync(
        CancellationToken cancellationToken = default);
}