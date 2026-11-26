using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.PublicCatalog;
using Revestik.Shared.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Revestik.Api.Authorization;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Authentication;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Authentication;

namespace Revestik.Api.Tests.Authentication;

public sealed class PublicIdentityCookieSqlTests(
    SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private RevestikWebApplicationFactory CreateFactory() =>
        new("Development",
            databaseConnectionString: fixture.ConnectionString,
            customizeTestServices: services =>
            {
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme =
                        IdentityConstants.ApplicationScheme;

                    options.DefaultChallengeScheme =
                        IdentityConstants.ApplicationScheme;
                });
            });

    private static void SetLogin(
        RevestikWebApplicationFactory factory,
        bool enabled)
    {
        var configuration = factory.Services
            .GetRequiredService<IConfiguration>();

        configuration[PublicAuthenticationRules.LoginEnabledKey] =
            enabled.ToString();

        configuration[PublicAuthenticationRules.RegistrationEnabledKey] =
            "false";
    }

    private static HttpClient CreateClient(
        RevestikWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });

    private static async Task<ApplicationUser> CreateUserAsync(
        RevestikWebApplicationFactory factory,
        params string[] roles)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                Assert.True((await roleManager.CreateAsync(
                    new IdentityRole(role))).Succeeded);
        }

        var email = $"cookie-{Guid.NewGuid():N}@example.test";

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsActive = true,
            DisplayName = "Cookie integration test",
            CreatedAtUtc = DateTime.UtcNow
        };

        Assert.True((await users.CreateAsync(user)).Succeeded);

        foreach (var role in roles)
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);

        return user;
    }

    private static async Task<string> IssueCookieAsync(
        RevestikWebApplicationFactory factory,
        string userId,
        DateTimeOffset issued,
        DateTimeOffset expires,
        bool publicSession)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var signIn = scope.ServiceProvider
            .GetRequiredService<SignInManager<ApplicationUser>>();

        var user = await users.FindByIdAsync(userId);
        Assert.NotNull(user);

        var principal = await signIn.CreateUserPrincipalAsync(user);

        var properties = publicSession
            ? GoogleLoginFlow.PublicSessionProperties(
                issued,
                GoogleLoginFlow.PublicSessionVersion(
                    factory.Services.GetRequiredService<IConfiguration>()))
            : new AuthenticationProperties();

        properties.IssuedUtc = issued;
        properties.ExpiresUtc = expires;
        properties.IsPersistent = false;

        var options = factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        var ticket = new AuthenticationTicket(
            principal, properties, IdentityConstants.ApplicationScheme);

        return $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}";
    }

    private static async Task<HttpResponseMessage> RequestAsync(
        HttpClient client, string cookie, string path = "/api/auth/me")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task ExistingCustomer_CanAccessWhenRegistrationIsClosed()
    {
        using var factory = CreateFactory();
        SetLogin(factory, true);

        var user = await CreateUserAsync(factory, RoleNames.PublicCustomer);
        var now = DateTimeOffset.UtcNow;

        var cookie = await IssueCookieAsync(
            factory, user.Id, now, now.AddHours(4), true);

        using var client = CreateClient(factory);
        using var response = await RequestAsync(client, cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PublicLoginShutdown_RejectsExistingCustomerCookie()
    {
        using var factory = CreateFactory();
        SetLogin(factory, true);

        var user = await CreateUserAsync(factory, RoleNames.PublicCustomer);
        var now = DateTimeOffset.UtcNow;

        var cookie = await IssueCookieAsync(
            factory, user.Id, now, now.AddHours(4), true);

        using var client = CreateClient(factory);

        using var before = await RequestAsync(client, cookie);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        SetLogin(factory, false);

        using var after = await RequestAsync(client, cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);

        Assert.True(after.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies, value =>
            value.StartsWith("__Host-Revestik.Auth=;",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PublicLoginShutdown_DoesNotRevokeAdministratorCookie()
    {
        using var factory = CreateFactory();
        SetLogin(factory, false);

        var user = await CreateUserAsync(factory, RoleNames.Administrator);
        var now = DateTimeOffset.UtcNow;

        var cookie = await IssueCookieAsync(
            factory, user.Id, now, now.AddHours(8), false);

        using var client = CreateClient(factory);

        using var response = await RequestAsync(client, cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var admin = await RequestAsync(
            client, cookie, "/api/settings/users");

        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
    }

    [Fact]
    public async Task RevokedSecurityStamp_RejectsPreviouslyIssuedCookie()
    {
        using var factory = CreateFactory();
        SetLogin(factory, true);

        var user = await CreateUserAsync(factory, RoleNames.PublicCustomer);
        var issued = DateTimeOffset.UtcNow.AddMinutes(-2);

        var cookie = await IssueCookieAsync(
            factory, user.Id, issued, issued.AddHours(4), true);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var current = await users.FindByIdAsync(user.Id);
            Assert.NotNull(current);

            current.IsActive = false;
            Assert.True((await users.UpdateAsync(current)).Succeeded);
            Assert.True((await users.UpdateSecurityStampAsync(current)).Succeeded);
        }

        using var client = CreateClient(factory);
        using var response = await RequestAsync(client, cookie);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredPublicCookie_IsRejected()
    {
        using var factory = CreateFactory();
        SetLogin(factory, true);

        var user = await CreateUserAsync(factory, RoleNames.PublicCustomer);
        var issued = DateTimeOffset.UtcNow.AddHours(-5);

        var cookie = await IssueCookieAsync(
            factory, user.Id, issued, issued.AddHours(4), true);

        using var client = CreateClient(factory);
        using var response = await RequestAsync(client, cookie);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MixedPublicAndAdministrativeRoles_AreRejected()
    {
        using var factory = CreateFactory();
        SetLogin(factory, true);

        var user = await CreateUserAsync(
            factory, RoleNames.PublicCustomer, RoleNames.Administrator);

        var now = DateTimeOffset.UtcNow;

        var cookie = await IssueCookieAsync(
            factory, user.Id, now, now.AddHours(4), true);

        using var client = CreateClient(factory);

        using var response = await RequestAsync(
            client, cookie, "/api/settings/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SecurityStampRefresh_DoesNotExtendPublicExpiry()
    {
        using var factory = CreateFactory();
        SetLogin(factory, true);

        var user = await CreateUserAsync(factory, RoleNames.PublicCustomer);

        var issued = DateTimeOffset.UtcNow.AddMinutes(-10);
        var expires = issued.AddHours(4);

        var cookie = await IssueCookieAsync(
            factory, user.Id, issued, expires, true);

        using var client = CreateClient(factory);
        using var response = await RequestAsync(client, cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.True(
            response.Headers.TryGetValues("Set-Cookie", out var cookies));

        var renewed = Assert.Single(cookies, value =>
            value.StartsWith("__Host-Revestik.Auth=",
                StringComparison.Ordinal));

        var value = renewed.Split(';', 2)[0].Split('=', 2)[1];

        var options = factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        var ticket = options.TicketDataFormat.Unprotect(value);

        Assert.NotNull(ticket);
        Assert.NotNull(ticket.Properties.ExpiresUtc);

        Assert.True(
            ticket.Properties.ExpiresUtc!.Value <= expires.AddSeconds(1),
            $"Expected expiry <= {expires:O}; actual " +
            $"{ticket.Properties.ExpiresUtc:O}");

        Assert.True(
            ticket.Properties.ExpiresUtc!.Value >= expires.AddSeconds(-1),
            "Cookie renewal must not unnecessarily shorten the session.");

        Assert.False(ticket.Properties.IsPersistent);
        Assert.False(ticket.Properties.AllowRefresh == true);

        Assert.NotNull(ticket.Properties.IssuedUtc);

        Assert.True(
            ticket.Properties.IssuedUtc!.Value > issued,
            "The validation timestamp must advance without extending expiry.");

        using var nextResponse = await RequestAsync(
            client,
            $"__Host-Revestik.Auth={value}");

        Assert.Equal(HttpStatusCode.OK, nextResponse.StatusCode);
    }

    [Fact]
    public async Task GlobalRevocation_OldCookieCannotReturnAfterReopening()
    {
        using var factory = CreateFactory();

        SetLogin(factory, true);

        var configuration = factory.Services
            .GetRequiredService<IConfiguration>();

        configuration[PublicAuthenticationRules.SessionVersionKey] = "1";

        var user = await CreateUserAsync(factory, RoleNames.PublicCustomer);

        var now = DateTimeOffset.UtcNow;

        var oldCookie = await IssueCookieAsync(
            factory, user.Id, now, now.AddHours(4), true);

        using var client = CreateClient(factory);

        using var before = await RequestAsync(client, oldCookie);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        SetLogin(factory, false);
        configuration[PublicAuthenticationRules.SessionVersionKey] = "2";

        using var closed = await RequestAsync(client, oldCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, closed.StatusCode);

        SetLogin(factory, true);

        using var replay = await RequestAsync(client, oldCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        var newCookie = await IssueCookieAsync(
            factory, user.Id, now, now.AddHours(4), true);

        using var reopened = await RequestAsync(client, newCookie);
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
    }

    [Fact]
    public async Task GlobalRevocation_DoesNotAffectEmployeeSession()
    {
        using var factory = CreateFactory();
        SetLogin(factory, true);

        var configuration = factory.Services
            .GetRequiredService<IConfiguration>();

        configuration[PublicAuthenticationRules.SessionVersionKey] = "1";

        var user = await CreateUserAsync(factory, RoleNames.Administrator);
        var now = DateTimeOffset.UtcNow;

        var cookie = await IssueCookieAsync(
            factory, user.Id, now, now.AddHours(8), false);

        SetLogin(factory, false);
        configuration[PublicAuthenticationRules.SessionVersionKey] = "2";

        using var client = CreateClient(factory);

        using var response = await RequestAsync(
            client, cookie, "/api/settings/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GlobalRevocation_RejectsCookieThatNeverSawShutdown()
    {
        using var factory = CreateFactory();
        SetLogin(factory, true);

        var configuration = factory.Services
            .GetRequiredService<IConfiguration>();

        configuration[PublicAuthenticationRules.SessionVersionKey] = "1";

        var user = await CreateUserAsync(factory, RoleNames.PublicCustomer);
        var now = DateTimeOffset.UtcNow;

        var cookie = await IssueCookieAsync(
            factory, user.Id, now, now.AddHours(4), true);

        // The browser makes no request during the shutdown.
        SetLogin(factory, false);
        configuration[PublicAuthenticationRules.SessionVersionKey] = "2";
        SetLogin(factory, true);

        using var client = CreateClient(factory);
        using var response = await RequestAsync(client, cookie);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task InconsistentAccount_DoesNotBreakEmployeeAdministration()
    {
        using var factory = CreateFactory();

        SetLogin(factory, false);

        var administrator = await CreateUserAsync(
            factory, RoleNames.Administrator);

        var inconsistent = await CreateUserAsync(
            factory,
            RoleNames.Administrator,
            RoleNames.PublicCustomer);

        var publicOnly = await CreateUserAsync(
            factory, RoleNames.PublicCustomer);

        var multipleInternal = await CreateUserAsync(
            factory, RoleNames.Sales, RoleNames.Warehouse);

        var now = DateTimeOffset.UtcNow;

        var cookie = await IssueCookieAsync(
            factory,
            administrator.Id,
            now,
            now.AddHours(8),
            false);

        using var client = CreateClient(factory);

        using var response = await RequestAsync(
            client, cookie, "/api/settings/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();

        var service = scope.ServiceProvider.GetRequiredService<
            Revestik.Api.Services.Users.UserAdministrationService>();

        var accounts = await service.ListAsync(CancellationToken.None);

        var validAdmin = Assert.Single(
            accounts, account => account.Id == administrator.Id);

        Assert.Equal(RoleNames.Administrator, validAdmin.Role);

        var mixed = Assert.Single(
            accounts, account => account.Id == inconsistent.Id);

        Assert.Equal(string.Empty, mixed.Role);

        var invalidEmployee = Assert.Single(
            accounts, account => account.Id == multipleInternal.Id);

        Assert.Equal(string.Empty, invalidEmployee.Role);

        Assert.DoesNotContain(
            accounts, account => account.Id == publicOnly.Id);

        using var detail = await RequestAsync(
            client,
            cookie,
            $"/api/settings/users/{inconsistent.Id}");

        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

        var invalidDetail = await service.GetAsync(
            inconsistent.Id,
            CancellationToken.None);

        Assert.NotNull(invalidDetail);
        Assert.Equal(string.Empty, invalidDetail.Role);

        var exception = await Assert.ThrowsAsync<
            Revestik.Api.Services.Users.UserAdministrationException>(
            () => service.UpdateAsync(
                inconsistent.Id,
                new Revestik.Shared.Users.UpdateManagedUserRequest
                {
                    DisplayName = mixed.DisplayName,
                    Role = RoleNames.Administrator,
                    IsActive = mixed.IsActive,
                    Version = mixed.Version
                },
                administrator.Id,
                CancellationToken.None));

        Assert.Equal(409, exception.StatusCode);

        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var unchanged = await users.FindByIdAsync(inconsistent.Id);

        Assert.NotNull(unchanged);

        var assignedRoles = await users.GetRolesAsync(unchanged);

        Assert.Equal(2, assignedRoles.Count);
        Assert.Contains(RoleNames.Administrator, assignedRoles);
        Assert.Contains(RoleNames.PublicCustomer, assignedRoles);
    }


    private static HttpClient CreateCookieClient(
        RevestikWebApplicationFactory factory,
        string cookie)
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = true
            });

        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "Cookie", cookie);

        return client;
    }

    private static async Task<string> GetCustomerAdminCsrfAsync(
        HttpClient client)
    {
        using var response = await client.GetAsync(
            "/api/auth/csrf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content
            .ReadFromJsonAsync<CsrfTokenResponse>();

        Assert.NotNull(payload);

        return payload.RequestToken;
    }

    private static async Task<HttpResponseMessage> ChangeCustomerStatusAsync(
        HttpClient client,
        string id,
        bool? active,
        string version,
        string csrf)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/settings/public-customers/{Uri.EscapeDataString(id)}/status")
        {
            Content = JsonContent.Create(
                new UpdatePublicCustomerStatusRequest
                {
                    IsActive = active,
                    Version = version
                })
        };

        request.Headers.Add("X-CSRF-TOKEN", csrf);

        return await client.SendAsync(request);
    }

    private async Task<ApplicationUser> GetCustomerAccountAsync(
        string id)
    {
        await using var db = fixture.CreateDbContext();

        return await db.Users.AsNoTracking()
            .SingleAsync(user => user.Id == id);
    }

    [Fact]
    public async Task PublicCustomerAdministration_ListIsRoleScoped()
    {
        using var factory = CreateFactory();

        SetLogin(factory, true);

        var administrator = await CreateUserAsync(
            factory, RoleNames.Administrator);

        var customer = await CreateUserAsync(
            factory, RoleNames.PublicCustomer);

        var employee = await CreateUserAsync(
            factory, RoleNames.Sales);

        var mixed = await CreateUserAsync(
            factory,
            RoleNames.PublicCustomer,
            RoleNames.Administrator);

        var noRole = await CreateUserAsync(factory);

        var now = DateTimeOffset.UtcNow;

        var cookie = await IssueCookieAsync(
            factory,
            administrator.Id,
            now,
            now.AddHours(8),
            false);

        using var client = CreateCookieClient(factory, cookie);

        var path =
            "/api/settings/public-customers?page=1&pageSize=100";

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);

        var result = await response.Content
            .ReadFromJsonAsync<PublicCustomerListResponse>();

        Assert.NotNull(result);

        Assert.Contains(
            result.Customers,
            item => item.Id == customer.Id);

        Assert.DoesNotContain(
            result.Customers,
            item => item.Id == employee.Id ||
                    item.Id == administrator.Id ||
                    item.Id == mixed.Id ||
                    item.Id == noRole.Id);

        var search = Uri.EscapeDataString(customer.Email!);

        using var filteredResponse = await client.GetAsync(
            path + "&search=" + search);

        Assert.Equal(
            HttpStatusCode.OK,
            filteredResponse.StatusCode);

        var filtered = await filteredResponse.Content
            .ReadFromJsonAsync<PublicCustomerListResponse>();

        Assert.NotNull(filtered);
        Assert.Equal(1, filtered.TotalCount);

        Assert.Equal(
            customer.Id,
            Assert.Single(filtered.Customers).Id);
    }

    [Fact]
    public async Task PublicCustomerAdministration_PaginationIsBounded()
    {
        using var factory = CreateFactory();

        var administrator = await CreateUserAsync(
            factory, RoleNames.Administrator);

        await CreateUserAsync(
            factory, RoleNames.PublicCustomer);

        await CreateUserAsync(
            factory, RoleNames.PublicCustomer);

        var now = DateTimeOffset.UtcNow;

        var cookie = await IssueCookieAsync(
            factory,
            administrator.Id,
            now,
            now.AddHours(8),
            false);

        using var client = CreateCookieClient(factory, cookie);

        var path = "/api/settings/public-customers";

        var first = await client.GetFromJsonAsync<
            PublicCustomerListResponse>(
                path + "?page=1&pageSize=1");

        var second = await client.GetFromJsonAsync<
            PublicCustomerListResponse>(
                path + "?page=2&pageSize=1");

        Assert.NotNull(first);
        Assert.NotNull(second);

        Assert.Equal(first.TotalCount, second.TotalCount);
        Assert.True(first.TotalCount >= 2);

        Assert.NotEqual(
            Assert.Single(first.Customers).Id,
            Assert.Single(second.Customers).Id);

        foreach (var query in new[]
        {
            "?page=0",
            "?page=-1",
            "?page=100001",
            "?pageSize=0",
            "?pageSize=101",
            "?search=" + new string('x', 201)
        })
        {
            using var response = await client.GetAsync(
                path + query);

            Assert.Equal(
                HttpStatusCode.BadRequest,
                response.StatusCode);
        }
    }

    [Fact]
    public async Task PublicCustomerAdministration_StatusChangesRevokeSessionsAndPreserveCart()
    {
        using var factory = CreateFactory();

        SetLogin(factory, true);

        var administrator = await CreateUserAsync(
            factory, RoleNames.Administrator);

        var customer = await CreateUserAsync(
            factory, RoleNames.PublicCustomer);

        var original = await GetCustomerAccountAsync(customer.Id);

        int productId;
        byte[] originalRowVersion;

        await using (var db = fixture.CreateDbContext())
        {
            var product = new PublicCatalogProduct
            {
                Slug = "customer-status-" + Guid.NewGuid().ToString("N"),
                IsPublished = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            db.PublicCatalogProducts.Add(product);
            await db.SaveChangesAsync();

            productId = product.Id;

            var item = new PublicCartItem
            {
                UserId = customer.Id,
                ProductId = productId,
                Quantity = 2.5m,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            db.PublicCartItems.Add(item);
            await db.SaveChangesAsync();

            originalRowVersion = item.RowVersion.ToArray();
        }

        var now = DateTimeOffset.UtcNow;
        var issued = now.AddMinutes(-2);

        var customerCookie = await IssueCookieAsync(
            factory,
            customer.Id,
            issued,
            issued.AddHours(4),
            true);

        var administratorCookie = await IssueCookieAsync(
            factory,
            administrator.Id,
            now,
            now.AddHours(8),
            false);

        using var admin = CreateCookieClient(
            factory, administratorCookie);

        using var customerClient = CreateClient(factory);

        var csrf = await GetCustomerAdminCsrfAsync(admin);

        using (var disabled = await ChangeCustomerStatusAsync(
            admin,
            customer.Id,
            false,
            original.ConcurrencyStamp!,
            csrf))
        {
            Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
            Assert.True(disabled.Headers.CacheControl?.NoStore);
        }

        var inactive = await GetCustomerAccountAsync(customer.Id);

        Assert.False(inactive.IsActive);

        Assert.NotEqual(
            original.SecurityStamp,
            inactive.SecurityStamp);

        Assert.NotEqual(
            original.ConcurrencyStamp,
            inactive.ConcurrencyStamp);

        using (var rejected = await RequestAsync(
            customerClient, customerCookie))
        {
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                rejected.StatusCode);
        }

        using (var stale = await ChangeCustomerStatusAsync(
            admin,
            customer.Id,
            true,
            original.ConcurrencyStamp!,
            csrf))
        {
            Assert.Equal(
                HttpStatusCode.Conflict,
                stale.StatusCode);
        }

        // Idempotent update must not change stamps or create audit noise.
        using (var unchanged = await ChangeCustomerStatusAsync(
            admin,
            customer.Id,
            false,
            inactive.ConcurrencyStamp!,
            csrf))
        {
            Assert.Equal(
                HttpStatusCode.OK,
                unchanged.StatusCode);
        }

        var afterNoOp = await GetCustomerAccountAsync(customer.Id);

        Assert.Equal(
            inactive.SecurityStamp,
            afterNoOp.SecurityStamp);

        Assert.Equal(
            inactive.ConcurrencyStamp,
            afterNoOp.ConcurrencyStamp);

        using (var reactivated = await ChangeCustomerStatusAsync(
            admin,
            customer.Id,
            true,
            inactive.ConcurrencyStamp!,
            csrf))
        {
            Assert.Equal(
                HttpStatusCode.OK,
                reactivated.StatusCode);
        }

        var active = await GetCustomerAccountAsync(customer.Id);

        Assert.True(active.IsActive);

        Assert.NotEqual(
            inactive.SecurityStamp,
            active.SecurityStamp);

        Assert.Equal(original.Email, active.Email);
        Assert.Equal(original.DisplayName, active.DisplayName);
        Assert.Equal(original.CreatedAtUtc, active.CreatedAtUtc);
        Assert.Equal(original.LastLoginAtUtc, active.LastLoginAtUtc);

        // A copied old cookie must remain invalid after reactivation.
        using (var replay = await RequestAsync(
            customerClient, customerCookie))
        {
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                replay.StatusCode);
        }

        var freshIssued = DateTimeOffset.UtcNow;

        var freshCookie = await IssueCookieAsync(
            factory,
            customer.Id,
            freshIssued,
            freshIssued.AddHours(4),
            true);

        using (var signedIn = await RequestAsync(
            customerClient, freshCookie))
        {
            Assert.Equal(
                HttpStatusCode.OK,
                signedIn.StatusCode);
        }

        await using var verify = fixture.CreateDbContext();

        var cart = await verify.PublicCartItems
            .AsNoTracking()
            .SingleAsync(item =>
                item.UserId == customer.Id &&
                item.ProductId == productId);

        Assert.Equal(2.5m, cart.Quantity);

        Assert.True(
            originalRowVersion.SequenceEqual(cart.RowVersion));

        var audits = await verify.Set<SecurityAuditEntry>()
            .AsNoTracking()
            .Where(entry =>
                entry.Action == "PublicCustomerStatusChanged" &&
                entry.EntityId == customer.Id)
            .OrderBy(entry => entry.Id)
            .ToArrayAsync();

        Assert.Equal(2, audits.Length);

        Assert.All(audits, entry =>
        {
            Assert.Equal(
                administrator.Id,
                entry.ActorUserId);

            Assert.Equal("User", entry.EntityType);
        });

        Assert.Contains(
            "\"active\":false",
            audits[0].Details!);

        Assert.Contains(
            "\"active\":true",
            audits[1].Details!);

        await using var scope = factory.Services.CreateAsyncScope();

        var manager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        Assert.Equal(
            RoleNames.PublicCustomer,
            Assert.Single(await manager.GetRolesAsync(active)));
    }

    [Fact]
    public async Task PublicCustomerAdministration_CannotModifyEmployeeOrMixedAccount()
    {
        using var factory = CreateFactory();

        var administrator = await CreateUserAsync(
            factory, RoleNames.Administrator);

        var employee = await CreateUserAsync(
            factory, RoleNames.Sales);

        var mixed = await CreateUserAsync(
            factory,
            RoleNames.PublicCustomer,
            RoleNames.Administrator);

        var customer = await CreateUserAsync(
            factory, RoleNames.PublicCustomer);

        var now = DateTimeOffset.UtcNow;

        var cookie = await IssueCookieAsync(
            factory,
            administrator.Id,
            now,
            now.AddHours(8),
            false);

        using var client = CreateCookieClient(factory, cookie);

        var csrf = await GetCustomerAdminCsrfAsync(client);

        foreach (var user in new[] { employee, mixed })
        {
            var before = await GetCustomerAccountAsync(user.Id);

            using var response = await ChangeCustomerStatusAsync(
                client,
                user.Id,
                false,
                before.ConcurrencyStamp!,
                csrf);

            Assert.Equal(
                HttpStatusCode.NotFound,
                response.StatusCode);

            var after = await GetCustomerAccountAsync(user.Id);

            Assert.Equal(before.IsActive, after.IsActive);
            Assert.Equal(before.SecurityStamp, after.SecurityStamp);
            Assert.Equal(before.ConcurrencyStamp, after.ConcurrencyStamp);
        }

        var current = await GetCustomerAccountAsync(customer.Id);

        using (var missingStatus = await ChangeCustomerStatusAsync(
            client,
            customer.Id,
            null,
            current.ConcurrencyStamp!,
            csrf))
        {
            Assert.Equal(
                HttpStatusCode.BadRequest,
                missingStatus.StatusCode);
        }

        using (var missingVersion = await ChangeCustomerStatusAsync(
            client,
            customer.Id,
            false,
            string.Empty,
            csrf))
        {
            Assert.Equal(
                HttpStatusCode.BadRequest,
                missingVersion.StatusCode);
        }

        var unchanged = await GetCustomerAccountAsync(customer.Id);

        Assert.True(unchanged.IsActive);

        Assert.Equal(
            current.SecurityStamp,
            unchanged.SecurityStamp);
    }


    [Theory]
    [InlineData("Administrator", "Sales")]
    [InlineData("Administrator", "UnknownRole")]
    [InlineData("UnknownRole", null)]
    [InlineData(null, null)]
    public async Task InvalidEmployeeRoleCombination_IsRejected(
        string? first, string? second)
    {
        using var factory = CreateFactory();
        SetLogin(factory, true);

        var roles = new[] { first, second }
            .OfType<string>()
            .ToArray();

        var user = await CreateUserAsync(factory, roles);

        var now = DateTimeOffset.UtcNow;

        var cookie = await IssueCookieAsync(
            factory,
            user.Id,
            now,
            now.AddHours(8),
            false);

        using var client = CreateClient(factory);

        using var account = await RequestAsync(
            client, cookie);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            account.StatusCode);

        using var admin = await RequestAsync(
            client, cookie, "/api/settings/public-customers");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            admin.StatusCode);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("2147483648")]
    [InlineData("")]
    public async Task InvalidPublicSessionConfiguration_DoesNotAffectEmployees(
        string invalidVersion)
    {
        using var factory = CreateFactory();
        SetLogin(factory, true);

        var configuration = factory.Services
            .GetRequiredService<IConfiguration>();

        configuration[PublicAuthenticationRules.SessionVersionKey] =
            "1";

        var customer = await CreateUserAsync(
            factory, RoleNames.PublicCustomer);

        var administrator = await CreateUserAsync(
            factory, RoleNames.Administrator);

        var now = DateTimeOffset.UtcNow;

        var customerCookie = await IssueCookieAsync(
            factory,
            customer.Id,
            now,
            now.AddHours(4),
            true);

        var employeeCookie = await IssueCookieAsync(
            factory,
            administrator.Id,
            now,
            now.AddHours(8),
            false);

        configuration[PublicAuthenticationRules.SessionVersionKey] =
            invalidVersion;

        using var client = CreateClient(factory);

        using var publicResponse = await RequestAsync(
            client, customerCookie);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            publicResponse.StatusCode);

        using var employeeResponse = await RequestAsync(
            client, employeeCookie,
            "/api/settings/public-customers");

        Assert.Equal(
            HttpStatusCode.OK,
            employeeResponse.StatusCode);
    }

    [Fact]
    public async Task ConcurrentCustomerStatusChanges_OnlyOneSucceeds()
    {
        using var factory = CreateFactory();
        SetLogin(factory, true);
        var customer = await CreateUserAsync(factory, RoleNames.PublicCustomer);
        var admin = await CreateUserAsync(factory, RoleNames.Administrator);
        var now = DateTimeOffset.UtcNow;
        var cookie = await IssueCookieAsync(factory, admin.Id, now, now.AddHours(8), false);
        using var first = CreateCookieClient(factory, cookie);
        using var second = CreateCookieClient(factory, cookie);
        var firstCsrf = await GetCustomerAdminCsrfAsync(first);
        var secondCsrf = await GetCustomerAdminCsrfAsync(second);
        var original = await GetCustomerAccountAsync(customer.Id);
        var version = original.ConcurrencyStamp!;
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Change(HttpClient client, string csrf)
        {
            await gate.Task;
            return await ChangeCustomerStatusAsync(client, customer.Id, false, version, csrf);
        }
        var a = Change(first, firstCsrf);
        var b = Change(second, secondCsrf);
        gate.SetResult(true);
        using var responseA = await a;
        using var responseB = await b;
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict },
            new[] { responseA.StatusCode, responseB.StatusCode }.OrderBy(x => (int)x).ToArray());
        Assert.False((await GetCustomerAccountAsync(customer.Id)).IsActive);
        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, await db.Set<SecurityAuditEntry>().CountAsync(x =>
            x.Action == "PublicCustomerStatusChanged" && x.EntityId == customer.Id));
    }

    [Theory]
    [InlineData("public", "1", true, 302)]
    [InlineData("internal", "2", true, 302)]
    [InlineData("public", "2", false, 404)]
    public async Task PublicCallback_RejectsObsoleteOrWrongIntentExternalTicket(
        string intent, string version, bool enabled, int status)
    {
        using var factory = CreateFactory();
        SetLogin(factory, enabled);
        var config = factory.Services.GetRequiredService<IConfiguration>();
        config[PublicAuthenticationRules.SessionVersionKey] = "2";
        config[PublicAuthenticationRules.RegistrationEnabledKey] = "true";
        var email = $"callback-{Guid.NewGuid():N}@example.test";
        var principal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("N")),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, email),
                new System.Security.Claims.Claim("urn:google:email_verified", "true")
            }, "Google"));
        var properties = new AuthenticationProperties();
        properties.Items["LoginProvider"] = "Google";
        properties.Items[PublicAuthenticationRules.SessionVersionProperty] = version;
        properties.IssuedUtc = DateTimeOffset.UtcNow;
        properties.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5);
        GoogleLoginFlow.SetIntent(properties, intent);
        GoogleLoginFlow.SetPublicLanguage(properties, "en");
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        var external = options.Get(IdentityConstants.ExternalScheme);
        var ticket = new AuthenticationTicket(principal, properties, IdentityConstants.ExternalScheme);
        var cookie = $"{external.Cookie.Name}={external.TicketDataFormat.Protect(ticket)}";
        using var client = CreateClient(factory);
        using var response = await RequestAsync(client, cookie, "/api/auth/public/external-callback");
        Assert.Equal(status, (int)response.StatusCode);
        if (enabled)
            Assert.Equal("/en?authError=external_login_failed", response.Headers.Location?.OriginalString);
        var application = options.Get(IdentityConstants.ApplicationScheme);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, x => x.StartsWith(external.Cookie.Name + "=", StringComparison.Ordinal));
        Assert.DoesNotContain(cookies!, x => x.StartsWith(application.Cookie.Name + "=", StringComparison.Ordinal));
        await using var db = fixture.CreateDbContext();
        Assert.False(await db.Users.AnyAsync(x => x.Email == email));
    }
}
