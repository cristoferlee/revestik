using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Revestik.Api.Data;

namespace Revestik.Api.Tests.Hosting;

public sealed class RevestikWebApplicationFactory(
    string environmentName,
    string? databaseConnectionString = null,
    string? projectFileStorageRoot = null,
    Action<IServiceCollection>? customizeTestServices = null)
    : WebApplicationFactory<Program>
{
    private readonly string inMemoryDatabaseName = $"RevestikHostingTests-{Guid.NewGuid()}";
    private readonly string productionStorageRoot = Path.Combine(
        Path.GetTempPath(), $"revestik-production-host-tests-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);
        builder.UseWebRoot(GetClientWebRoot());
        builder.UseSetting("ConnectionStrings:RevestikDatabase",
            databaseConnectionString ?? "Server=(local);Database=RevestikHostingTests;");

        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            string? testCertificateBase64 = null;
            if (environmentName == "Production")
            {
                Directory.CreateDirectory(productionStorageRoot);
                using var rsa = RSA.Create(2048);
                var request = new CertificateRequest(
                    "CN=Revestik-ProductionHostingTests",
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);
                using var certificate = request.CreateSelfSigned(
                    DateTimeOffset.UtcNow.AddDays(-1),
                    DateTimeOffset.UtcNow.AddDays(2));
                testCertificateBase64 = Convert.ToBase64String(
                    certificate.Export(X509ContentType.Pkcs12, "test-only-password"));
            }

            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RevestikStorage:RootPath"] = environmentName == "Production"
                    ? productionStorageRoot : null,
                ["DataProtection:CertificateBase64"] = testCertificateBase64,
                ["DataProtection:CertificatePassword"] = environmentName == "Production"
                    ? "test-only-password" : null,
                ["ConnectionStrings:RevestikDatabase"] = databaseConnectionString ??
                    "Server=(local);Database=RevestikHostingTests;",
                ["AllowedOrigins:0"] = "https://localhost:7081",
                ["Authentication:ClientBaseUrl"] = "https://localhost:7081",
                ["ReloadStaticAssetsAtRuntime"] = "false",
                ["ProjectFileStorage:RootPath"] = projectFileStorageRoot ??
                    Path.Combine(Path.GetTempPath(), "revestik-test-files-" + inMemoryDatabaseName)
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<RevestikDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<RevestikDbContext>>();
            services.RemoveAll<IDatabaseProvider>();
            services.RemoveAll<RevestikDbContext>();
            services.AddDbContext<RevestikDbContext>(options =>
            {
                if (databaseConnectionString is null)
                    options.UseInMemoryDatabase(inMemoryDatabaseName);
                else options.UseSqlServer(databaseConnectionString);
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName, _ => { });
            customizeTestServices?.Invoke(services);
        });
    }

    private static string GetClientWebRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Revestik.sln")))
            directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("The repository root could not be located.");
        return Path.Combine(directory.FullName, "src", "Revestik.Client", "wwwroot");
    }
}
