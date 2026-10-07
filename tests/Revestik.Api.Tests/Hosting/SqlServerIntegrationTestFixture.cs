using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Testcontainers.MsSql;

namespace Revestik.Api.Tests.Hosting;

public sealed class SqlServerIntegrationTestFixture : IAsyncLifetime
{
    private static readonly MsSqlContainer Container =
        new MsSqlBuilder(
            "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04")
            .WithDatabase("RevestikIntegrationTestsHost")
            .Build();

    private static readonly SemaphoreSlim StartupLock = new(1, 1);

    private static bool containerStarted;

    private readonly string databaseName =
        $"RevestikIntegrationTests_{Guid.NewGuid():N}";

    public string ConnectionString
    {
        get
        {
            var builder = new SqlConnectionStringBuilder(
                Container.GetConnectionString())
            {
                InitialCatalog = databaseName
            };

            return builder.ConnectionString;
        }
    }

    public async Task InitializeAsync()
    {
        await EnsureContainerStartedAsync();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync();
    }

    public RevestikDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<RevestikDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;

        return new RevestikDbContext(options);
    }

    private static async Task EnsureContainerStartedAsync()
    {
        if (containerStarted)
        {
            return;
        }

        await StartupLock.WaitAsync();

        try
        {
            if (containerStarted)
            {
                return;
            }

            await Container.StartAsync();
            containerStarted = true;
        }
        finally
        {
            StartupLock.Release();
        }
    }
}