using System.Data.Common;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Models.Identity;

namespace Revestik.Api.Tests.Hosting;

public sealed class HealthEndpointSqlTests
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private readonly SqlServerIntegrationTestFixture fixture;

    public HealthEndpointSqlTests(
        SqlServerIntegrationTestFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task PublicHealth_Anonymous_DoesNotAccessSql()
    {
        var counter = new SqlActivityCounter();

        using var factory = CreateFactory(counter);
        using var client = factory.CreateClient();

        counter.Reset();

        using var response =
            await client.GetAsync("/api/health");

        await AssertPublicHealthAsync(response);

        Assert.Equal(0, counter.Connections);
        Assert.Equal(0, counter.Commands);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicHealth_RealIdentityCookie_DoesNotAccessSql(
        bool validationDue)
    {
        var counter = new SqlActivityCounter();

        using var factory = CreateFactory(counter);
        using var client = factory.CreateClient();

        var session = await CreateSessionAsync(
            factory,
            RoleNames.Administrator,
            validationDue);

        counter.Reset();

        using var request =
            CreateRequest("/api/health", session.Cookie);

        using var response = await client.SendAsync(request);

        await AssertPublicHealthAsync(response);

        Assert.Equal(0, counter.Connections);
        Assert.Equal(0, counter.Commands);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task PublicHealth_InvalidCookie_DoesNotAccessSql()
    {
        var counter = new SqlActivityCounter();

        using var factory = CreateFactory(counter);
        using var client = factory.CreateClient();

        counter.Reset();

        using var request = CreateRequest(
            "/api/health",
            "__Host-Revestik.Auth=invalid-ticket");

        using var response = await client.SendAsync(request);

        await AssertPublicHealthAsync(response);

        Assert.Equal(0, counter.Connections);
        Assert.Equal(0, counter.Commands);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task DatabaseHealth_Anonymous_Returns401()
    {
        var counter = new SqlActivityCounter();

        using var factory = CreateFactory(counter);
        using var client = factory.CreateClient();

        counter.Reset();

        using var response =
            await client.GetAsync("/api/health/database");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);

        AssertNoStore(response);
        Assert.Equal(0, counter.Connections);
        Assert.Equal(0, counter.Commands);
    }

    [Fact]
    public async Task DatabaseHealth_NonAdministrator_Returns403()
    {
        var counter = new SqlActivityCounter();

        using var factory = CreateFactory(counter);
        using var client = factory.CreateClient();

        var session = await CreateSessionAsync(
            factory,
            RoleNames.Sales,
            validationDue: false);

        counter.Reset();

        using var request = CreateRequest(
            "/api/health/database",
            session.Cookie);

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);

        AssertNoStore(response);
        Assert.Equal(0, counter.Connections);
        Assert.Equal(0, counter.Commands);
    }

    [Fact]
    public async Task DatabaseHealth_Administrator_ChecksSql()
    {
        var counter = new SqlActivityCounter();

        using var factory = CreateFactory(counter);
        using var client = factory.CreateClient();

        var session = await CreateSessionAsync(
            factory,
            RoleNames.Administrator,
            validationDue: false);

        counter.Reset();

        using var request = CreateRequest(
            "/api/health/database",
            session.Cookie);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoStore(response);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "Connected",
            document.RootElement
                .GetProperty("database").GetString());

        Assert.True(counter.Connections > 0);
    }

    [Fact]
    public async Task RevokedCookie_PublicHealthDoesNotValidate_ProtectedHealthRejects()
    {
        var counter = new SqlActivityCounter();

        using var factory = CreateFactory(counter);
        using var client = factory.CreateClient();

        var session = await CreateSessionAsync(
            factory,
            RoleNames.Administrator,
            validationDue: true);

        await using (var scope =
            factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var user = await users.FindByIdAsync(session.UserId);

            Assert.NotNull(user);

            Assert.True(
                (await users.UpdateSecurityStampAsync(user))
                .Succeeded);
        }

        counter.Reset();

        using (var publicRequest =
            CreateRequest("/api/health", session.Cookie))
        using (var publicResponse =
            await client.SendAsync(publicRequest))
        {
            await AssertPublicHealthAsync(publicResponse);

            Assert.Equal(0, counter.Connections);
            Assert.Equal(0, counter.Commands);
            Assert.False(
                publicResponse.Headers.Contains("Set-Cookie"));
        }

        using var protectedRequest = CreateRequest(
            "/api/health/database",
            session.Cookie);

        using var protectedResponse =
            await client.SendAsync(protectedRequest);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            protectedResponse.StatusCode);

        AssertNoStore(protectedResponse);

        // Positive control: actual Identity validation accessed SQL.
        Assert.True(counter.Connections > 0);
        Assert.True(counter.Commands > 0);
    }

    [Fact]
    public async Task PublicHealth_RateLimit_Returns429_WithoutSql()
    {
        var counter = new SqlActivityCounter();

        using var factory = CreateFactory(counter);
        using var client = factory.CreateClient();

        counter.Reset();

        for (var i = 0; i < 60; i++)
        {
            using var response =
                await client.GetAsync("/api/health");

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);

            AssertNoStore(response);
        }

        using var rejected =
            await client.GetAsync("/api/health");

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            rejected.StatusCode);

        AssertNoStore(rejected);
        Assert.True(rejected.Headers.Contains("Retry-After"));

        Assert.Equal(0, counter.Connections);
        Assert.Equal(0, counter.Commands);
    }

    private RevestikWebApplicationFactory CreateFactory(
        SqlActivityCounter counter)
    {
        return new RevestikWebApplicationFactory(
            "Development",
            databaseConnectionString: fixture.ConnectionString,
            customizeTestServices: services =>
            {
                services.Configure<AuthenticationOptions>(options =>
                {
                    options.DefaultScheme =
                        IdentityConstants.ApplicationScheme;

                    options.DefaultAuthenticateScheme =
                        IdentityConstants.ApplicationScheme;

                    options.DefaultChallengeScheme =
                        IdentityConstants.ApplicationScheme;

                    options.DefaultForbidScheme =
                        IdentityConstants.ApplicationScheme;
                });

                services.AddDbContext<RevestikDbContext>(
                    options => options.AddInterceptors(
                        counter.ConnectionInterceptor,
                        counter.CommandInterceptor));
            });
    }

    private static async Task<(string UserId, string Cookie)>
        CreateSessionAsync(
            RevestikWebApplicationFactory factory,
            string role,
            bool validationDue)
    {
        await using var scope =
            factory.Services.CreateAsyncScope();

        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var roles = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        var signIn = scope.ServiceProvider
            .GetRequiredService<SignInManager<ApplicationUser>>();

        if (!await roles.RoleExistsAsync(role))
        {
            Assert.True(
                (await roles.CreateAsync(new IdentityRole(role)))
                .Succeeded);
        }

        var email = $"health-{Guid.NewGuid():N}@example.test";

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Health endpoint test",
            IsActive = true
        };

        Assert.True((await users.CreateAsync(user)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);

        var principal =
            await signIn.CreateUserPrincipalAsync(user);

        var options = scope.ServiceProvider
            .GetRequiredService<
                IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        var now = DateTimeOffset.UtcNow;

        var properties = new AuthenticationProperties
        {
            IssuedUtc = validationDue
                ? now.AddMinutes(-2)
                : now,
            ExpiresUtc = now.AddHours(4)
        };

        var ticket = new AuthenticationTicket(
            principal,
            properties,
            IdentityConstants.ApplicationScheme);

        var cookie =
            options.Cookie.Name + "=" +
            options.TicketDataFormat.Protect(ticket);

        return (user.Id, cookie);
    }

    private static HttpRequestMessage CreateRequest(
        string path,
        string cookie)
    {
        var request =
            new HttpRequestMessage(HttpMethod.Get, path);

        request.Headers.TryAddWithoutValidation(
            "Cookie", cookie);

        return request;
    }

    private static async Task AssertPublicHealthAsync(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoStore(response);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = document.RootElement;

        Assert.Equal(
            "Healthy",
            root.GetProperty("status").GetString());

        Assert.Equal(
            "Revestik.Api",
            root.GetProperty("service").GetString());

        Assert.True(root.TryGetProperty("timestampUtc", out _));
        Assert.False(root.TryGetProperty("database", out _));
    }

    private static void AssertNoStore(
        HttpResponseMessage response)
    {
        Assert.True(
            response.Headers.CacheControl?.NoStore == true);
    }

    private sealed class SqlActivityCounter
    {
        private int connections;
        private int commands;

        public int Connections =>
            Volatile.Read(ref connections);

        public int Commands =>
            Volatile.Read(ref commands);

        public DbConnectionInterceptor ConnectionInterceptor { get; }

        public DbCommandInterceptor CommandInterceptor { get; }

        public SqlActivityCounter()
        {
            ConnectionInterceptor = new ConnectionCounter(this);
            CommandInterceptor = new CommandCounter(this);
        }

        public void Reset()
        {
            Interlocked.Exchange(ref connections, 0);
            Interlocked.Exchange(ref commands, 0);
        }

        private sealed class ConnectionCounter(
            SqlActivityCounter owner) : DbConnectionInterceptor
        {
            public override InterceptionResult ConnectionOpening(
                DbConnection connection,
                ConnectionEventData eventData,
                InterceptionResult result)
            {
                Interlocked.Increment(ref owner.connections);
                return result;
            }

            public override ValueTask<InterceptionResult>
                ConnectionOpeningAsync(
                    DbConnection connection,
                    ConnectionEventData eventData,
                    InterceptionResult result,
                    CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref owner.connections);
                return ValueTask.FromResult(result);
            }
        }

        private sealed class CommandCounter(
            SqlActivityCounter owner) : DbCommandInterceptor
        {
            public override InterceptionResult<DbDataReader>
                ReaderExecuting(
                    DbCommand command,
                    CommandEventData eventData,
                    InterceptionResult<DbDataReader> result)
            {
                Interlocked.Increment(ref owner.commands);
                return result;
            }

            public override ValueTask<InterceptionResult<DbDataReader>>
                ReaderExecutingAsync(
                    DbCommand command,
                    CommandEventData eventData,
                    InterceptionResult<DbDataReader> result,
                    CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref owner.commands);
                return ValueTask.FromResult(result);
            }

            public override InterceptionResult<int> NonQueryExecuting(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<int> result)
            {
                Interlocked.Increment(ref owner.commands);
                return result;
            }

            public override ValueTask<InterceptionResult<int>>
                NonQueryExecutingAsync(
                    DbCommand command,
                    CommandEventData eventData,
                    InterceptionResult<int> result,
                    CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref owner.commands);
                return ValueTask.FromResult(result);
            }

            public override InterceptionResult<object> ScalarExecuting(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<object> result)
            {
                Interlocked.Increment(ref owner.commands);
                return result;
            }

            public override ValueTask<InterceptionResult<object>>
                ScalarExecutingAsync(
                    DbCommand command,
                    CommandEventData eventData,
                    InterceptionResult<object> result,
                    CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref owner.commands);
                return ValueTask.FromResult(result);
            }
        }
    }
}
