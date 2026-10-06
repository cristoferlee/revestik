using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Revestik.Api.Configuration;

namespace Revestik.Api.Services.GmailIntegration;

internal sealed class GmailIntegrationStateStore : IGmailIntegrationStateStore
{
    private const string ProtectorPurpose = "Revestik.GmailIntegration.State.v1";

    private readonly IDataProtector protector;
    private readonly string statePath;
    private readonly SemaphoreSlim gate = new(1, 1);

    public GmailIntegrationStateStore(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<GmailIntegrationOptions> options,
        IWebHostEnvironment environment)
    {
        protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);

        var configuredPath = options.Value.StateStoragePath;
        statePath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);

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
