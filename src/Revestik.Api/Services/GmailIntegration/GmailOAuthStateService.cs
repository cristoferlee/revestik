using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Revestik.Api.Services.GmailIntegration;

internal interface IGmailOAuthStateService
{
    string Create(string userId);
    void Validate(string protectedState, string userId);
}

internal interface IBankVoucherGmailOAuthStateService
{
    string Create(string userId);
    void Validate(string protectedState, string userId);
}

internal sealed class GmailOAuthStateService : IGmailOAuthStateService
{
    private readonly ProtectedGmailOAuthStateService inner;

    public GmailOAuthStateService(IDataProtectionProvider dataProtectionProvider)
    {
        inner = new ProtectedGmailOAuthStateService(
            dataProtectionProvider,
            "Revestik.GmailIntegration.OAuthState.v1");
    }

    public string Create(string userId) => inner.Create(userId);

    public void Validate(string protectedState, string userId) =>
        inner.Validate(protectedState, userId);
}

internal sealed class BankVoucherGmailOAuthStateService
    : IBankVoucherGmailOAuthStateService
{
    private readonly ProtectedGmailOAuthStateService inner;

    public BankVoucherGmailOAuthStateService(
        IDataProtectionProvider dataProtectionProvider)
    {
        inner = new ProtectedGmailOAuthStateService(
            dataProtectionProvider,
            "Revestik.BankVoucherGmailIntegration.OAuthState.v1");
    }

    public string Create(string userId) => inner.Create(userId);

    public void Validate(string protectedState, string userId) =>
        inner.Validate(protectedState, userId);
}

internal sealed class ProtectedGmailOAuthStateService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private readonly IDataProtector protector;

    public ProtectedGmailOAuthStateService(
        IDataProtectionProvider dataProtectionProvider,
        string protectorPurpose)
    {
        protector = dataProtectionProvider.CreateProtector(protectorPurpose);
    }

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
