using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Testcontainers.MsSql;

namespace Revestik.Api.Tests.Hosting;

public sealed class SqlServerIntegrationTestFixture : IAsyncLifetime
{
    private readonly MsSqlContainer container =
        new MsSqlBuilder(
            "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04")
            .WithDatabase("RevestikIntegrationTests")
            .Build();

    public string ConnectionString => container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await container.StartAsync();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await container.DisposeAsync();
    }

    public RevestikDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RevestikDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new RevestikDbContext(options);
    }
}
