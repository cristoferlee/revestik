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
    string environmentName)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);
        builder.UseWebRoot(GetClientWebRoot());
        builder.UseSetting(
            "ConnectionStrings:RevestikDatabase",
            "Server=(local);Database=RevestikHostingTests;");

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:RevestikDatabase"] =
                        "Server=(local);Database=RevestikHostingTests;",
                    ["AllowedOrigins:0"] =
                        "https://localhost:7081",
                    ["Authentication:ClientBaseUrl"] =
                        "https://localhost:7081",
                    ["ReloadStaticAssetsAtRuntime"] = "false"
                });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<
                DbContextOptions<RevestikDbContext>>();
            services.RemoveAll<
                IDbContextOptionsConfiguration<RevestikDbContext>>();
            services.RemoveAll<IDatabaseProvider>();
            services.RemoveAll<RevestikDbContext>();

            services.AddDbContext<RevestikDbContext>(options =>
            {
                options.UseInMemoryDatabase(
                    $"RevestikHostingTests-{Guid.NewGuid()}");
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme =
                        TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme =
                        TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<
                    AuthenticationSchemeOptions,
                    TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ =>
                    {
                    });
        });
    }

    private static string GetClientWebRoot()
    {
        var directory = new DirectoryInfo(
            AppContext.BaseDirectory);

        while (directory is not null &&
               !File.Exists(Path.Combine(
                   directory.FullName,
                   "Revestik.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                "The repository root could not be located.");
        }

        return Path.Combine(
            directory.FullName,
            "src",
            "Revestik.Client",
            "wwwroot");
    }
}
