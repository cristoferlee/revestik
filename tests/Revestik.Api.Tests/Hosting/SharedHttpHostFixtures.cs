using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Revestik.Api.Tests.Hosting;

/// <summary>Shares a test host within each xUnit class.</summary>
public sealed class DevelopmentHttpHostFixture : IAsyncLifetime
{
    public RevestikWebApplicationFactory Factory { get; } = new("Development");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await Factory.DisposeAsync();
}

/// <summary>
/// Top-level Program checks production configuration before WebApplicationFactory
/// can contribute ConfigureAppConfiguration values. Set isolated test-only process
/// configuration before constructing the production host, and restore it afterward.
/// This collection is nonparallel to prevent environment-variable leakage.
/// </summary>
public sealed class ProductionHttpHostFixture : IAsyncLifetime
{
    private const string RootKey = "RevestikStorage__RootPath";
    private const string CertificateKey = "DataProtection__CertificateBase64";
    private const string PasswordKey = "DataProtection__CertificatePassword";

    private readonly Dictionary<string, string?> originalValues = new();
    private readonly string storageRoot;

    public RevestikWebApplicationFactory Factory { get; }

    public ProductionHttpHostFixture()
    {
        storageRoot = Path.Combine(Path.GetTempPath(),
            "revestik-production-host-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storageRoot);

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Revestik-HostingTest", rsa,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));

        try
        {
            SetTemporary(RootKey, storageRoot);
            SetTemporary(CertificateKey, Convert.ToBase64String(
                certificate.Export(X509ContentType.Pkcs12, "revestik-test-only")));
            SetTemporary(PasswordKey, "revestik-test-only");
            Factory = new RevestikWebApplicationFactory("Production");
        }
        catch
        {
            Restore();
            Directory.Delete(storageRoot, recursive: true);
            throw;
        }
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await Factory.DisposeAsync();
        }
        finally
        {
            Restore();
            if (Directory.Exists(storageRoot))
                Directory.Delete(storageRoot, recursive: true);
        }
    }

    private void SetTemporary(string key, string value)
    {
        originalValues[key] = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
    }

    private void Restore()
    {
        foreach (var item in originalValues)
            Environment.SetEnvironmentVariable(item.Key, item.Value);
    }
}
