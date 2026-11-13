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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Revestik.Api.Data;
using Revestik.Api.Services.PublicCatalog;

namespace Revestik.Api.Tests.Hosting;

public sealed class RevestikWebApplicationFactory(
    string environmentName,
    string? databaseConnectionString = null,
    string? projectFileStorageRoot = null,
    Action<IServiceCollection>? customizeTestServices = null)
    : WebApplicationFactory<Program>
{
    private readonly string inMemoryDatabaseName =
        $"RevestikHostingTests-{Guid.NewGuid()}";

    private readonly string productionStorageRoot = Path.Combine(
        Path.GetTempPath(),
        $"revestik-production-host-tests-{Guid.NewGuid():N}");

    // Unique per-host media storage. Test databases must never share App_Data with development.
    private readonly string publicMediaTestRoot = Path.Combine(
        Path.GetTempPath(),
        $"revestik-media-host-tests-{Guid.NewGuid():N}");

    public string PublicMediaTestRoot => publicMediaTestRoot;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);

        // Use the appropriate static asset structure for the environment.
        builder.UseWebRoot(GetClientWebRoot(environmentName));

        builder.UseSetting(
            "ConnectionStrings:RevestikDatabase",
            databaseConnectionString ??
            "Server=(local);Database=RevestikHostingTests;");

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
                    certificate.Export(
                        X509ContentType.Pkcs12,
                        "test-only-password"));
            }

            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["RevestikStorage:RootPath"] =
                        environmentName == "Production"
                            ? productionStorageRoot
                            : null,

                    ["DataProtection:CertificateBase64"] =
                        testCertificateBase64,

                    ["DataProtection:CertificatePassword"] =
                        environmentName == "Production"
                            ? "test-only-password"
                            : null,

                    ["ConnectionStrings:RevestikDatabase"] =
                        databaseConnectionString ??
                        "Server=(local);Database=RevestikHostingTests;",

                    ["AllowedOrigins:0"] =
                        "https://localhost:7081",

                    ["Authentication:ClientBaseUrl"] =
                        "https://localhost:7081",

                    ["ReloadStaticAssetsAtRuntime"] =
                        "false",

                    ["ProjectFileStorage:RootPath"] =
                        projectFileStorageRoot ??
                        Path.Combine(
                            Path.GetTempPath(),
                            "revestik-test-files-" + inMemoryDatabaseName)
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
                {
                    options.UseInMemoryDatabase(inMemoryDatabaseName);
                }
                else
                {
                    options.UseSqlServer(databaseConnectionString);
                }
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // A test database does not contain the live media references. Prevent the
            // production cleanup worker from pruning any files during test startup.
            var cleanupRegistration = services.SingleOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(PublicCatalogMediaCleanupWorker));
            if (cleanupRegistration is null)
                throw new InvalidOperationException("Expected the public media cleanup registration in the test host.");
            services.Remove(cleanupRegistration);

            // Even direct storage calls made by test endpoints must stay isolated.
            services.RemoveAll<PublicCatalogMediaStorage>();
            services.AddSingleton(new PublicCatalogMediaStorage(publicMediaTestRoot));

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme =
                    TestAuthenticationHandler.SchemeName;

                options.DefaultChallengeScheme =
                    TestAuthenticationHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions,
                TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });

            customizeTestServices?.Invoke(services);
        });
    }

    private static string GetClientWebRoot(string environmentName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null &&
               !File.Exists(
                   Path.Combine(directory.FullName, "Revestik.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                "The repository root could not be located.");
        }

        var source = Path.Combine(
            directory.FullName,
            "src",
            "Revestik.Client",
            "wwwroot");

        if (environmentName != "Production")
        {
            return source;
        }

        // Reproduce the published /administracion/ directory structure
        // for production hosting tests.
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            "revestik-hosting-" + Guid.NewGuid().ToString("N"));

        var destination = Path.Combine(
            testRoot,
            "administracion");

        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);

            var target = Path.Combine(
                destination,
                relative);

            Directory.CreateDirectory(
                Path.GetDirectoryName(target)!);

            File.Copy(file, target);
        }

        return testRoot;
    }
}