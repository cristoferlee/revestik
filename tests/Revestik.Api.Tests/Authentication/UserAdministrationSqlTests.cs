using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Revestik.Api.Authorization;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Users;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Users;

namespace Revestik.Api.Tests.Authentication;

public sealed class UserAdministrationSqlTests : IClassFixture<SqlServerIntegrationTestFixture>
{
    private readonly SqlServerIntegrationTestFixture fixture;

    public UserAdministrationSqlTests(SqlServerIntegrationTestFixture fixture) => this.fixture = fixture;

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
        {
            if (!await manager.RoleExistsAsync(name))
                Assert.True((await manager.CreateAsync(new IdentityRole(name))).Succeeded);
        }
    }

    [Fact]
    public async Task Create_PersistsOneRole_AndRejectsDuplicateEmail()
    {
        await using var provider = BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var service = scope.ServiceProvider.GetRequiredService<UserAdministrationService>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureRolesAsync(roles);
        var email = $"config03-{Guid.NewGuid():N}@example.test";
        var request = new CreateManagedUserRequest { Email = email, DisplayName = "Prueba", Role = RoleNames.Sales };
        var created = await service.CreateAsync(request, "test-admin", CancellationToken.None);
        Assert.True(created.IsActive);
        Assert.Equal(RoleNames.Sales, created.Role);
        var saved = await users.FindByEmailAsync(email);
        Assert.NotNull(saved);
        Assert.Equal([RoleNames.Sales], await users.GetRolesAsync(saved));
        var error = await Assert.ThrowsAsync<UserAdministrationException>(
            () => service.CreateAsync(request, "test-admin", CancellationToken.None));
        Assert.Equal(409, error.StatusCode);
    }

    [Fact]
    public async Task Update_ProtectsLastActiveAdministrator_AndRejectsStaleVersion()
    {
        await using var provider = BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var service = scope.ServiceProvider.GetRequiredService<UserAdministrationService>();
        await EnsureRolesAsync(roles);
        var admin = await service.CreateAsync(new CreateManagedUserRequest
        {
            Email = $"admin-{Guid.NewGuid():N}@example.test", DisplayName = "Admin", Role = RoleNames.Administrator
        }, "seed", CancellationToken.None);
        var failure = await Assert.ThrowsAsync<UserAdministrationException>(() => service.UpdateAsync(
            admin.Id, new UpdateManagedUserRequest
            {
                DisplayName = "Admin", Role = RoleNames.Sales, IsActive = true, Version = admin.Version
            }, "other-admin", CancellationToken.None));
        Assert.Equal(409, failure.StatusCode);
        var stale = await Assert.ThrowsAsync<UserAdministrationException>(() => service.UpdateAsync(
            admin.Id, new UpdateManagedUserRequest
            {
                DisplayName = "Admin", Role = RoleNames.Administrator, IsActive = true, Version = "stale"
            }, "other-admin", CancellationToken.None));
        Assert.Equal(409, stale.StatusCode);
    }

    [Fact]
    public async Task Update_ChangesRoleAndSecurityStamp_ThenRejectsOldVersion()
    {
        await using var provider = BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var service = scope.ServiceProvider.GetRequiredService<UserAdministrationService>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureRolesAsync(roles);
        var created = await service.CreateAsync(new CreateManagedUserRequest
        {
            Email = $"employee-{Guid.NewGuid():N}@example.test", DisplayName = "Empleado", Role = RoleNames.Sales
        }, "admin", CancellationToken.None);
        var original = await users.FindByIdAsync(created.Id);
        Assert.NotNull(original);
        var initialStamp = await users.GetSecurityStampAsync(original);
        var request = new UpdateManagedUserRequest
        {
            DisplayName = "Empleado actualizado", Role = RoleNames.Warehouse,
            IsActive = false, Version = created.Version
        };
        var updated = await service.UpdateAsync(created.Id, request, "admin", CancellationToken.None);
        Assert.Equal(RoleNames.Warehouse, updated.Role);
        Assert.False(updated.IsActive);
        var saved = await users.FindByIdAsync(created.Id);
        Assert.NotNull(saved);
        Assert.Equal([RoleNames.Warehouse], await users.GetRolesAsync(saved));
        Assert.NotEqual(initialStamp, await users.GetSecurityStampAsync(saved));
        var conflict = await Assert.ThrowsAsync<UserAdministrationException>(() =>
            service.UpdateAsync(created.Id, request, "admin", CancellationToken.None));
        Assert.Equal(409, conflict.StatusCode);
    }

    [Fact]
    public async Task Create_RejectsUnknownRoleWithoutPersistingAccount()
    {
        await using var provider = BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<UserAdministrationService>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"invalid-{Guid.NewGuid():N}@example.test";
        var error = await Assert.ThrowsAsync<UserAdministrationException>(() => service.CreateAsync(
            new CreateManagedUserRequest { Email = email, DisplayName = "Test", Role = "Staff" },
            "admin", CancellationToken.None));
        Assert.Equal(400, error.StatusCode);
        Assert.Null(await users.FindByEmailAsync(email));
    }
}
