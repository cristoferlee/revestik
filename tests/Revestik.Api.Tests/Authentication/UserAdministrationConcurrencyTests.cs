using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Revestik.Api.Authorization;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Users;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Users;

namespace Revestik.Api.Tests.Authentication;

public sealed class UserAdministrationConcurrencyTests : IClassFixture<SqlServerIntegrationTestFixture>
{
    private readonly SqlServerIntegrationTestFixture fixture;
    public UserAdministrationConcurrencyTests(SqlServerIntegrationTestFixture fixture) => this.fixture = fixture;
    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<Revestik.Api.Data.RevestikDbContext>(options =>
            options.UseSqlServer(fixture.ConnectionString));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<Revestik.Api.Data.RevestikDbContext>();
        services.AddScoped<UserAdministrationService>();
        return services.BuildServiceProvider();
    }
    private static async Task EnsureRolesAsync(RoleManager<IdentityRole> manager)
    {
        foreach (var name in RoleNames.All)
            if (!await manager.RoleExistsAsync(name))
                Assert.True((await manager.CreateAsync(new IdentityRole(name))).Succeeded);
    }


    [Fact]
    public async Task ConcurrentUpdates_CannotDeactivateBothActiveAdministrators()
    {
        await using var provider = BuildServices();
        await using (var scope = provider.CreateAsyncScope())
        {
            await EnsureRolesAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
        }
        ManagedUserResponse first;
        ManagedUserResponse second;
        await using (var scope = provider.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<UserAdministrationService>();
            first = await service.CreateAsync(new CreateManagedUserRequest
            {
                Email = $"concurrent-admin-a-{Guid.NewGuid():N}@example.test",
                DisplayName = "Admin A", Role = RoleNames.Administrator
            }, "bootstrap", CancellationToken.None);
            second = await service.CreateAsync(new CreateManagedUserRequest
            {
                Email = $"concurrent-admin-b-{Guid.NewGuid():N}@example.test",
                DisplayName = "Admin B", Role = RoleNames.Administrator
            }, "bootstrap", CancellationToken.None);
        }

        async Task<bool> AttemptAsync(ManagedUserResponse target)
        {
            await using var scope = provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<UserAdministrationService>();
            try
            {
                await service.UpdateAsync(target.Id, new UpdateManagedUserRequest
                {
                    DisplayName = target.DisplayName,
                    Role = RoleNames.Administrator,
                    IsActive = false,
                    Version = target.Version
                }, "external-operator", CancellationToken.None);
                return true;
            }
            catch (UserAdministrationException ex) when (ex.StatusCode == 409)
            {
                return false;
            }
        }

        var outcomes = await Task.WhenAll(
            Task.Run(() => AttemptAsync(first)),
            Task.Run(() => AttemptAsync(second)));
        Assert.Single(outcomes, success => success);

        await using var verify = fixture.CreateDbContext();
        var roleId = await verify.Roles.Where(r => r.Name == RoleNames.Administrator)
            .Select(r => r.Id).SingleAsync();
        var activeCount = await (from u in verify.Users
            join ur in verify.UserRoles on u.Id equals ur.UserId
            where ur.RoleId == roleId && u.IsActive
            select u.Id).CountAsync();
        Assert.Equal(1, activeCount);
    }


}
