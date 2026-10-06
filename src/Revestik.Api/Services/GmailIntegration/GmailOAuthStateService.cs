using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Revestik.Api.Services.GmailIntegration;

internal interface IGmailOAuthStateService
{
    string Create(string userId);
    void Validate(string protectedState, string userId);
}

internal sealed class GmailOAuthStateService(
    IDataProtectionProvider dataProtectionProvider)
    : IGmailOAuthStateService
{
    private const string ProtectorPurpose = "Revestik.GmailIntegration.OAuthState.v1";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly IDataProtector protector =
        dataProtectionProvider.CreateProtector(ProtectorPurpose);

    public string Create(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id is required.", nameof(userId));

        var payload = new OAuthStatePayload(
            userId,
            Guid.NewGuid().ToString("N"),
            DateTime.UtcNow.Add(Lifetime));

        return protector.Protect(JsonSerializer.Serialize(payload));
    }

    public void Validate(string protectedState, string userId)
    {
        if (string.IsNullOrWhiteSpace(protectedState))
            throw new GmailIntegrationException("El estado OAuth de Google no es válido.");

        try
        {
            var json = protector.Unprotect(protectedState);
            var payload = JsonSerializer.Deserialize<OAuthStatePayload>(json)
                ?? throw new GmailIntegrationException("El estado OAuth de Google no es válido.");

            if (!string.Equals(payload.UserId, userId, StringComparison.Ordinal))
                throw new GmailIntegrationException("La autorización de Gmail pertenece a otra sesión.");

            if (payload.ExpiresAtUtc < DateTime.UtcNow)
                throw new GmailIntegrationException("La autorización de Gmail expiró. Inicia la conexión nuevamente.");

            if (string.IsNullOrWhiteSpace(payload.Nonce))
                throw new GmailIntegrationException("El estado OAuth de Google no es válido.");
        }
        catch (GmailIntegrationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is System.Security.Cryptography.CryptographicException
            or JsonException)
        {
            throw new GmailIntegrationException("El estado OAuth de Google no es válido.");
        }
    }

    private sealed record OAuthStatePayload(
        string UserId,
        string Nonce,
        DateTime ExpiresAtUtc);
}
