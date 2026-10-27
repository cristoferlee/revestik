using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Services.Audit;
using Revestik.Api.Models.Identity;
using Revestik.Shared.Users;

namespace Revestik.Api.Services.Users;

public sealed class UserAdministrationService(
    RevestikDbContext db,
    UserManager<ApplicationUser> userManager,
    ILogger<UserAdministrationService> logger)
{
    public async Task<IReadOnlyList<ManagedUserResponse>> ListAsync(CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking()
            .OrderBy(u => u.Email).ToListAsync(ct);
        var userIds = users.Select(u => u.Id).ToArray();
        var roleLinks = await (from link in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on link.RoleId equals role.Id
            where userIds.Contains(link.UserId)
            select new { link.UserId, Role = role.Name }).ToListAsync(ct);
        var roles = roleLinks.GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Role ?? string.Empty).ToArray());
        return users.Select(u => Map(u, roles.GetValueOrDefault(u.Id) ?? [])).ToArray();
    }

    public async Task<ManagedUserResponse?> GetAsync(string id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return null;
        return Map(user, await userManager.GetRolesAsync(user));
    }

    public async Task<ManagedUserResponse> CreateAsync(CreateManagedUserRequest request, string actorId, CancellationToken ct)
    {
        ValidateRole(request.Role);
        var email = (request.Email ?? string.Empty).Trim();
        var displayName = (request.DisplayName ?? string.Empty).Trim();
        if (email.Length == 0 || email.Length > 256 || displayName.Length is < 1 or > 150 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            throw new UserAdministrationException("Correo o nombre no válido.", 400);
        var user = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            DisplayName = displayName, IsActive = true, LockoutEnabled = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new UserAdministrationException("Ya existe un usuario con ese correo.", 409);
        EnsureIdentitySuccess(await userManager.CreateAsync(user));
        EnsureIdentitySuccess(await userManager.AddToRoleAsync(user, request.Role));
        SecurityAudit.Record(db, "UserCreated", "User", user.Id, actorId,
            new { role = request.Role, active = true });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("User {UserId} created by {ActorId} with role {Role}.", user.Id, actorId, request.Role);
        return Map(user, [request.Role]);
    }

    public async Task<ManagedUserResponse> UpdateAsync(
        string id, UpdateManagedUserRequest request, string actorId, CancellationToken ct)
    {
        ValidateRole(request.Role);
        var name = (request.DisplayName ?? string.Empty).Trim();
        if (name.Length is < 1 or > 150)
            throw new UserAdministrationException("Nombre no válido.", 400);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // Serialize all administration writes by locking the shared Administrator role row.
        var adminRole = await db.Roles.FromSqlRaw(
            "SELECT * FROM AspNetRoles WITH (UPDLOCK, HOLDLOCK) WHERE NormalizedName = {0}",
            RoleNames.Administrator.ToUpperInvariant()).SingleAsync(ct);
        var user = await userManager.FindByIdAsync(id)
            ?? throw new UserAdministrationException("Usuario no encontrado.", 404);
        if (!string.Equals(user.ConcurrencyStamp, request.Version, StringComparison.Ordinal))
            throw new UserAdministrationException("El usuario cambió. Actualizá la pantalla.", 409);
        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Count != 1 || !RoleNames.All.Contains(currentRoles[0], StringComparer.Ordinal))
            throw new UserAdministrationException("La cuenta tiene roles inconsistentes y requiere revisión.", 409);
        var currentRole = currentRoles[0];
        var removingAdmin = currentRole == RoleNames.Administrator &&
            (!request.IsActive || request.Role != RoleNames.Administrator);
        if (removingAdmin)
        {
            var adminRoleId = adminRole.Id;
            var count = await (from u in db.Users
                join ur in db.UserRoles on u.Id equals ur.UserId
                where ur.RoleId == adminRoleId && u.IsActive
                select u.Id).CountAsync(ct);
            if (count <= 1)
                throw new UserAdministrationException("No se puede retirar al último administrador activo.", 409);
        }
        if (user.Id == actorId && (!request.IsActive || request.Role != RoleNames.Administrator))
            throw new UserAdministrationException("No podés quitarte el acceso administrativo desde tu sesión.", 409);

        var oldName = user.DisplayName;
        var oldActive = user.IsActive;
        user.DisplayName = name;
        user.IsActive = request.IsActive;
        EnsureIdentitySuccess(await userManager.UpdateAsync(user));
        if (currentRole != request.Role)
        {
            EnsureIdentitySuccess(await userManager.RemoveFromRoleAsync(user, currentRole));
            EnsureIdentitySuccess(await userManager.AddToRoleAsync(user, request.Role));
        }
        // Force cookie revalidation to reject old role or active-status claims.
        EnsureIdentitySuccess(await userManager.UpdateSecurityStampAsync(user));
        if (oldName != name || oldActive != request.IsActive || currentRole != request.Role)
        {
            SecurityAudit.Record(db, "UserUpdated", "User", user.Id, actorId, new
            {
                previousName = oldName, newName = name,
                previousRole = currentRole, newRole = request.Role,
                previousActive = oldActive, newActive = request.IsActive
            });
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        logger.LogInformation("User {UserId} updated by {ActorId}: role {OldRole} -> {NewRole}, active {IsActive}.",
            user.Id, actorId, currentRole, request.Role, user.IsActive);
        return Map(user, [request.Role]);
    }

    private static ManagedUserResponse Map(ApplicationUser user, IEnumerable<string> roles) =>
        new(user.Id, user.Email ?? string.Empty, user.DisplayName,
            roles.SingleOrDefault() ?? string.Empty, user.IsActive,
            user.CreatedAtUtc, user.LastLoginAtUtc, user.ConcurrencyStamp ?? string.Empty);

    private static void ValidateRole(string role)
    {
        if (role is null || !RoleNames.All.Contains(role, StringComparer.Ordinal))
            throw new UserAdministrationException("Rol no válido.", 400);
    }

    private static void EnsureIdentitySuccess(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new UserAdministrationException("No se pudo guardar la cuenta. Revisá los datos y volvé a intentar.", 409);
    }
}

public sealed class UserAdministrationException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
