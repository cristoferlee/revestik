using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Revestik.Api.Services.GmailIntegration;

internal sealed class ProtectedGmailIntegrationStateStore
{
    private readonly IDataProtector protector;
    private readonly string statePath;
    private readonly SemaphoreSlim gate = new(1, 1);

    public ProtectedGmailIntegrationStateStore(
        IDataProtectionProvider dataProtectionProvider,
        IWebHostEnvironment environment,
        string stateStoragePath,
        string protectorPurpose)
    {
        protector = dataProtectionProvider.CreateProtector(protectorPurpose);

        statePath = Path.IsPathRooted(stateStoragePath)
            ? stateStoragePath
            : Path.Combine(environment.ContentRootPath, stateStoragePath);

        var directory = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }

    public async Task<GmailIntegrationState> LoadAsync(
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(statePath))
                return new GmailIntegrationState();

            var protectedPayload = await File.ReadAllTextAsync(
                statePath,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(protectedPayload))
                return new GmailIntegrationState();

            try
            {
                var json = protector.Unprotect(protectedPayload);
                return JsonSerializer.Deserialize<GmailIntegrationState>(json)
                    ?? new GmailIntegrationState();
            }
            catch (Exception exception) when (
                exception is System.Security.Cryptography.CryptographicException
                or JsonException)
            {
                throw new GmailIntegrationException(
                    "No fue posible leer de forma segura la credencial de Gmail. Vuelve a conectar la cuenta.");
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAsync(
        GmailIntegrationState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);

        await gate.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(state);
            var protectedPayload = protector.Protect(json);

            var tempPath = statePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, protectedPayload, cancellationToken);
            File.Move(tempPath, statePath, overwrite: true);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(statePath))
                File.Delete(statePath);
        }
        finally
        {
            gate.Release();
        }
    }
}
