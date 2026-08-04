using Microsoft.AspNetCore.Identity;
using Revestik.Api.Authorization;

namespace Revestik.Api.Data;

public static class IdentitySeeder
{
    public static async Task InitializeIdentityAsync(
        this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        var roleManager =
            scope.ServiceProvider.GetRequiredService<
                RoleManager<IdentityRole>>();

        foreach (var roleName in RoleNames.Obsolete)
        {
            var obsoleteRole =
                await roleManager.FindByNameAsync(roleName);

            if (obsoleteRole is null)
            {
                continue;
            }

            var result = await roleManager.DeleteAsync(obsoleteRole);

            if (!result.Succeeded)
            {
                ThrowRoleOperationException(
                    roleName,
                    "deleted",
                    result.Errors);
            }
        }

        foreach (var roleName in RoleNames.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(
                new IdentityRole(roleName));

            if (!result.Succeeded)
            {
                ThrowRoleOperationException(
                    roleName,
                    "created",
                    result.Errors);
            }
        }
    }

    private static void ThrowRoleOperationException(
        string roleName,
        string operation,
        IEnumerable<IdentityError> identityErrors)
    {
        var errors = string.Join(
            "; ",
            identityErrors.Select(error => error.Description));

        throw new InvalidOperationException(
            $"Role '{roleName}' could not be {operation}: {errors}");
    }
}