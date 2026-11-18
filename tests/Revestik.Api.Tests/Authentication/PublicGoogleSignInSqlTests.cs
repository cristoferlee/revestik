using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Authentication;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.Authentication;

public sealed class PublicGoogleSignInSqlTests : IClassFixture<SqlServerIntegrationTestFixture>
{
    private readonly SqlServerIntegrationTestFixture fixture;

    public PublicGoogleSignInSqlTests(SqlServerIntegrationTestFixture fixture) =>
        this.fixture = fixture;

    private ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<RevestikDbContext>(o => o.UseSqlServer(fixture.ConnectionString));
        services.AddIdentityCore<ApplicationUser>(o => o.User.RequireUniqueEmail = true)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<RevestikDbContext>();
        services.AddScoped<PublicGoogleSignInService>();
        return services.BuildServiceProvider();
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole> roles)
    {
        foreach (var role in RoleNames.All.Append(RoleNames.PublicCustomer))
            if (!await roles.RoleExistsAsync(role))
                Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
    }

    [Fact]
    public async Task Registration_CreatesOnlyPublicRoleAndBindsProviderSubject()
    {
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureRolesAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
        var service = scope.ServiceProvider.GetRequiredService<PublicGoogleSignInService>();
        var email = $"public-{Guid.NewGuid():N}@example.test";
        var login = Login(email, $"subject-{Guid.NewGuid():N}");

        var created = await service.ResolveAsync(login, CancellationToken.None);
        Assert.Equal(PublicGoogleAccountStatus.Created, created.Status);
        Assert.NotNull(created.User);
        Assert.True(created.User.EmailConfirmed);
        Assert.Equal("Public visitor", created.User.DisplayName);
        Assert.Equal([RoleNames.PublicCustomer], await manager.GetRolesAsync(created.User));
        Assert.Equal(created.User.Id,
            (await manager.FindByLoginAsync("Google", login.ProviderKey))?.Id);

        var repeat = await service.ResolveAsync(login, CancellationToken.None);
        Assert.Equal(PublicGoogleAccountStatus.Existing, repeat.Status);
        Assert.Equal(created.User.Id, repeat.User?.Id);
    }

    [Fact]
    public async Task SameEmailWithDifferentGoogleSubject_FailsClosed()
    {
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        await EnsureRolesAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
        var service = scope.ServiceProvider.GetRequiredService<PublicGoogleSignInService>();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"collision-{Guid.NewGuid():N}@example.test";

        var first = await service.ResolveAsync(Login(email, "sub-one"), CancellationToken.None);
        var second = await service.ResolveAsync(Login(email, "sub-two"), CancellationToken.None);
        Assert.Equal(PublicGoogleAccountStatus.Created, first.Status);
        Assert.Equal(PublicGoogleAccountStatus.Conflict, second.Status);
        Assert.Null(await manager.FindByLoginAsync("Google", "sub-two"));
    }

    [Fact]
    public async Task PreAuthorizedEmployee_IsNeverLinkedToPublicGoogleSubject()
    {
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        await EnsureRolesAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var service = scope.ServiceProvider.GetRequiredService<PublicGoogleSignInService>();
        var email = $"staff-{Guid.NewGuid():N}@example.test";
        var staff = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            IsActive = true, DisplayName = "Staff"
        };
        Assert.True((await manager.CreateAsync(staff)).Succeeded);
        Assert.True((await manager.AddToRoleAsync(staff, RoleNames.Sales)).Succeeded);

        var attempted = await service.ResolveAsync(Login(email, "new-public-subject"), CancellationToken.None);
        Assert.Equal(PublicGoogleAccountStatus.Conflict, attempted.Status);
        Assert.Null(await manager.FindByLoginAsync("Google", "new-public-subject"));
        Assert.Equal([RoleNames.Sales], await manager.GetRolesAsync(staff));
    }

    [Fact]
    public async Task DisabledPublicAccount_CannotSignInAgain()
    {
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        await EnsureRolesAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var service = scope.ServiceProvider.GetRequiredService<PublicGoogleSignInService>();
        var email = $"disabled-{Guid.NewGuid():N}@example.test";
        var login = Login(email, $"sub-{Guid.NewGuid():N}");
        var first = await service.ResolveAsync(login, CancellationToken.None);
        Assert.Equal(PublicGoogleAccountStatus.Created, first.Status);
        first.User!.IsActive = false;
        Assert.True((await manager.UpdateAsync(first.User)).Succeeded);

        var blocked = await service.ResolveAsync(login, CancellationToken.None);
        Assert.Equal(PublicGoogleAccountStatus.Disabled, blocked.Status);
    }

    [Fact]
    public async Task SimultaneousFirstSignups_DoNotCreateTwoAccounts()
    {
        await using var provider = Services();
        await using (var initialScope = provider.CreateAsyncScope())
        {
            await EnsureRolesAsync(initialScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
        }

        var email = $"simultaneous-{Guid.NewGuid():N}@example.test";
        var login = Login(email, $"simultaneous-sub-{Guid.NewGuid():N}");
        async Task<PublicGoogleAccountStatus> Attempt()
        {
            await using var scope = provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<PublicGoogleSignInService>();
            return (await service.ResolveAsync(login, CancellationToken.None)).Status;
        }

        var attempts = await Task.WhenAll(Attempt(), Attempt());
        Assert.Contains(PublicGoogleAccountStatus.Created, attempts);
        Assert.All(attempts, status => Assert.Contains(status,
            new[] { PublicGoogleAccountStatus.Created, PublicGoogleAccountStatus.Existing,
                    PublicGoogleAccountStatus.Conflict }));

        await using var verificationScope = provider.CreateAsyncScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<RevestikDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(user => user.Email == email));
        var manager = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var saved = await manager.FindByEmailAsync(email);
        Assert.NotNull(saved);
        Assert.Equal([RoleNames.PublicCustomer], await manager.GetRolesAsync(saved));
    }

    [Fact]
    public async Task UnverifiedGoogleEmail_IsRejectedWithoutWritingUser()
    {
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var service = scope.ServiceProvider.GetRequiredService<PublicGoogleSignInService>();
        var email = $"unverified-{Guid.NewGuid():N}@example.test";
        Assert.Equal(PublicGoogleAccountStatus.InvalidIdentity, (await service.ResolveAsync(
            Login(email, "unverified-sub", "false"), CancellationToken.None)).Status);
        Assert.Null(await manager.FindByEmailAsync(email));
    }

    private static ExternalLoginInfo Login(string email, string subject, string verified = "true") =>
        new(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Name, "Public visitor"),
                new Claim("urn:google:email_verified", verified)
            ], "Google")), "Google", subject, "Google");
}
