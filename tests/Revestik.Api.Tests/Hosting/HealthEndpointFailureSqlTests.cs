using System.Data.Common;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Models.Identity;

namespace Revestik.Api.Tests.Hosting;

public sealed class HealthEndpointFailureSqlTests
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private readonly SqlServerIntegrationTestFixture fixture;

    public HealthEndpointFailureSqlTests(SqlServerIntegrationTestFixture fixture)
        => this.fixture = fixture;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicHealth_WhenSqlConnectionFails_RemainsAvailable(bool withCookie)
    {
        var fault = new ConnectionFault();
        using var factory = CreateFactory(fault);
        using var client = CreateClient(factory);
        var cookie = withCookie ? await CreateCookieAsync(factory, true) : null;
        fault.Enable();

        using var request = Request("/api/health", cookie);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoStore(response);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", json.RootElement.GetProperty("status").GetString());
        Assert.False(json.RootElement.TryGetProperty("database", out _));
        Assert.Equal(0, fault.Attempts);
    }

    [Fact]
    public async Task DatabaseHealth_WhenSqlConnectionFails_Returns503()
    {
        var fault = new ConnectionFault();
        using var factory = CreateFactory(fault);
        using var client = CreateClient(factory);
        // A fresh, genuine administrator ticket isolates connectivity diagnosis
        // from a SecurityStamp revalidation that would itself need SQL.
        var cookie = await CreateCookieAsync(factory, false);
        fault.Enable();

        using var request = Request("/api/health/database", cookie);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        AssertNoStore(response);
        Assert.True(fault.Attempts > 0);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(503, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Database connection failed.", json.RootElement.GetProperty("title").GetString());
        Assert.False((await response.Content.ReadAsStringAsync()).Contains(ConnectionFault.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task DatabaseHealth_SharesHealthRateLimit_AndRejectsBeforeDiagnosis()
    {
        var fault = new ConnectionFault();
        using var factory = CreateFactory(fault);
        using var client = CreateClient(factory);
        var cookie = await CreateCookieAsync(factory, false);
        fault.Enable();

        // Consume 59 permits through liveness and the last through diagnosis.
        // Both routes use the same existing IP-based health policy.
        for (var i = 0; i < 59; i++)
        {
            using var response = await client.GetAsync("/api/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertNoStore(response);
        }
        Assert.Equal(0, fault.Attempts);

        using (var request = Request("/api/health/database", cookie))
        using (var response = await client.SendAsync(request))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            AssertNoStore(response);
        }
        var attempts = fault.Attempts;
        Assert.True(attempts > 0);

        using (var request = Request("/api/health/database", cookie))
        using (var response = await client.SendAsync(request))
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            AssertNoStore(response);
            Assert.True(response.Headers.Contains("Retry-After"));
        }
        using (var response = await client.GetAsync("/api/health"))
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            AssertNoStore(response);
        }
        Assert.Equal(attempts, fault.Attempts);
    }

    private RevestikWebApplicationFactory CreateFactory(ConnectionFault fault)
        => new("Development", databaseConnectionString: fixture.ConnectionString,
            customizeTestServices: services =>
            {
                services.Configure<AuthenticationOptions>(options =>
                {
                    options.DefaultScheme = IdentityConstants.ApplicationScheme;
                    options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                    options.DefaultForbidScheme = IdentityConstants.ApplicationScheme;
                });
                services.AddDbContext<RevestikDbContext>(options => options.AddInterceptors(fault));
            });

    private static HttpClient CreateClient(RevestikWebApplicationFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });

    private static async Task<string> CreateCookieAsync(RevestikWebApplicationFactory factory, bool validationDue)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        if (!await roles.RoleExistsAsync(RoleNames.Administrator))
            Assert.True((await roles.CreateAsync(new IdentityRole(RoleNames.Administrator))).Succeeded);

        var email = $"health-fault-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            DisplayName = "Health fault test", IsActive = true
        };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, RoleNames.Administrator)).Succeeded);
        var principal = await signIn.CreateUserPrincipalAsync(user);
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        var now = DateTimeOffset.UtcNow;
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            IssuedUtc = validationDue ? now.AddMinutes(-2) : now,
            ExpiresUtc = now.AddHours(4)
        }, IdentityConstants.ApplicationScheme);
        return options.Cookie.Name + "=" + options.TicketDataFormat.Protect(ticket);
    }

    private static HttpRequestMessage Request(string path, string? cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (cookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private static void AssertNoStore(HttpResponseMessage response)
        => Assert.True(response.Headers.CacheControl?.NoStore == true);

    private sealed class ConnectionFault : DbConnectionInterceptor
    {
        internal const string Message = "Simulated SQL connection failure (test only).";
        private int enabled;
        private int attempts;
        public int Attempts => Volatile.Read(ref attempts);
        public void Enable() => Volatile.Write(ref enabled, 1);

        private void FailIfEnabled()
        {
            if (Volatile.Read(ref enabled) == 0) return;
            Interlocked.Increment(ref attempts);
            throw new InvalidOperationException(Message);
        }

        public override InterceptionResult ConnectionOpening(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result)
        {
            FailIfEnabled();
            return result;
        }

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            FailIfEnabled();
            return ValueTask.FromResult(result);
        }
    }
}
