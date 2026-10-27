using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Models.Identity;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.Authentication;

public sealed class UserAdministrationSecurityStampTests : IClassFixture<SqlServerIntegrationTestFixture>
{
    private readonly SqlServerIntegrationTestFixture fixture;
    public UserAdministrationSecurityStampTests(SqlServerIntegrationTestFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task ChangingSecurityStamp_RejectsPreviouslyIssuedPrincipal()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddDbContext<RevestikDbContext>(options =>
            options.UseSqlServer(fixture.ConnectionString));
        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<RevestikDbContext>()
            .AddDefaultTokenProviders();
        using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        if (!await roles.RoleExistsAsync(RoleNames.Sales))
            Assert.True((await roles.CreateAsync(new IdentityRole(RoleNames.Sales))).Succeeded);
        var email = $"stamp-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            DisplayName = "Security stamp test", IsActive = true
        };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, RoleNames.Sales)).Succeeded);
        var existingPrincipal = await signIn.CreateUserPrincipalAsync(user);
        Assert.NotNull(await signIn.ValidateSecurityStampAsync(existingPrincipal));
        Assert.True((await users.UpdateSecurityStampAsync(user)).Succeeded);
        Assert.Null(await signIn.ValidateSecurityStampAsync(existingPrincipal));
        var replacementPrincipal = await signIn.CreateUserPrincipalAsync(user);
        Assert.NotNull(await signIn.ValidateSecurityStampAsync(replacementPrincipal));
    }
}
