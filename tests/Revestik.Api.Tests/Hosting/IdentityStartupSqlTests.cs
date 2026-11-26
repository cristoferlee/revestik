using System.Data.Common;
using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Revestik.Api.Authorization;
using Revestik.Api.Data;

namespace Revestik.Api.Tests.Hosting;

public sealed class IdentityStartupSqlTests
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private readonly SqlServerIntegrationTestFixture fixture;

    public IdentityStartupSqlTests(SqlServerIntegrationTestFixture fixture)
        => this.fixture = fixture;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductionStartup_AndPublicHealth_DoNotAccessSql(bool failConnections)
    {
        // Instrument before creating the host, not after startup. The existing
        // test factory isolates storage and removes the monthly media worker.
        var connections = new StartupConnections(failConnections);
        var commands = new StartupCommands();
        using var factory = CreateFactory("Production", connections, commands);
        using var client = CreateClient(factory);

        Assert.Equal(0, connections.Count);
        Assert.Equal(0, commands.Count);

        using var response = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        Assert.Equal(0, connections.Count);
        Assert.Equal(0, commands.Count);
    }

    [Fact]
    public async Task DevelopmentStartup_StillInitializesIdentityRoles()
    {
        var connections = new StartupConnections(false);
        var commands = new StartupCommands();
        using var factory = CreateFactory("Development", connections, commands);
        using var client = CreateClient(factory);

        // Positive control: the same interceptors observe the existing seeder.
        Assert.True(connections.Count > 0);
        Assert.True(commands.Count > 0);

        await using var scope = factory.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var name in RoleNames.All.Append(RoleNames.PublicCustomer))
            Assert.True(await roles.RoleExistsAsync(name), $"Missing role: {name}");
    }

    private RevestikWebApplicationFactory CreateFactory(
        string environment, StartupConnections connections, StartupCommands commands)
        => new(environment, databaseConnectionString: fixture.ConnectionString,
            customizeTestServices: services =>
                services.AddDbContext<RevestikDbContext>(options =>
                    options.AddInterceptors(connections, commands)));

    private static HttpClient CreateClient(RevestikWebApplicationFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });

    private sealed class StartupConnections(bool failConnections) : DbConnectionInterceptor
    {
        private int count;
        public int Count => Volatile.Read(ref count);

        private void Observe()
        {
            Interlocked.Increment(ref count);
            if (failConnections)
                throw new InvalidOperationException("Simulated startup SQL failure (test only).");
        }

        public override InterceptionResult ConnectionOpening(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result)
        {
            Observe();
            return result;
        }

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            Observe();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StartupCommands : DbCommandInterceptor
    {
        private int count;
        public int Count => Volatile.Read(ref count);
        private void Observe() => Interlocked.Increment(ref count);

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Observe();
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Observe();
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result)
        {
            Observe();
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Observe();
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<object> ScalarExecuting(DbCommand command,
            CommandEventData eventData, InterceptionResult<object> result)
        {
            Observe();
            return result;
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            Observe();
            return ValueTask.FromResult(result);
        }
    }
}
