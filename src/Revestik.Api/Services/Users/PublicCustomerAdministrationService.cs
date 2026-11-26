using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Audit;
using Revestik.Api.Services.Authentication;
using Revestik.Shared.Users;

namespace Revestik.Api.Services.Users;

public sealed class PublicCustomerAdministrationService(
    RevestikDbContext db,
    UserManager<ApplicationUser> userManager)
{
    public async Task<PublicCustomerListResponse> ListAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        search = search?.Trim();

        if (page < 1 || page > 100000 ||
            pageSize < 1 || pageSize > 100 ||
            search?.Length > 200)
        {
            throw new UserAdministrationException(
                "Parametros de consulta no validos.", 400);
        }

        var query = PublicCustomers();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(user =>
                user.DisplayName.Contains(search) ||
                (user.Email != null && user.Email.Contains(search)));
        }

        var total = await query.CountAsync(ct);

        var customers = await query
            .OrderByDescending(user => user.CreatedAtUtc)
            .ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new PublicCustomerAccountResponse(
                user.Id,
                user.Email ?? string.Empty,
                user.DisplayName,
                user.IsActive,
                user.CreatedAtUtc,
                user.LastLoginAtUtc,
                user.ConcurrencyStamp ?? string.Empty))
            .ToListAsync(ct);

        return new PublicCustomerListResponse(
            customers, page, pageSize, total);
    }

    public async Task<PublicCustomerAccountResponse> UpdateStatusAsync(
        string id,
        UpdatePublicCustomerStatusRequest request,
        string actorId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 450 ||
            request.IsActive is null ||
            string.IsNullOrWhiteSpace(request.Version) ||
            request.Version.Length > 256)
        {
            throw new UserAdministrationException(
                "Solicitud no valida.", 400);
        }

        await using var transaction =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, ct);

        // Acquire an update lock before reading the concurrency stamp.
        var user = await db.Users.FromSqlInterpolated(
            $"SELECT * FROM AspNetUsers WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id}")
            .SingleOrDefaultAsync(ct);

        if (user is null ||
            !GoogleLoginFlow.HasPublicRole(
                await userManager.GetRolesAsync(user)))
        {
            throw new UserAdministrationException(
                "Cuenta publica no encontrada.", 404);
        }

        if (!string.Equals(
            user.ConcurrencyStamp,
            request.Version,
            StringComparison.Ordinal))
        {
            throw new UserAdministrationException(
                "La cuenta cambio. Actualiza la pantalla.", 409);
        }

        var requestedActive = request.IsActive.Value;

        if (user.IsActive == requestedActive)
        {
            await transaction.CommitAsync(ct);
            return Map(user);
        }

        var previousActive = user.IsActive;

        user.IsActive = requestedActive;

        // UpdateSecurityStampAsync also persists the current user.
        // This revokes all previously issued Identity sessions for
        // this customer and updates the concurrency stamp.
        var result = await userManager.UpdateSecurityStampAsync(user);

        if (!result.Succeeded)
        {
            throw new UserAdministrationException(
                "No se pudo actualizar la cuenta.", 409);
        }

        SecurityAudit.Record(
            db,
            "PublicCustomerStatusChanged",
            "User",
            user.Id,
            actorId,
            new
            {
                previousActive,
                active = requestedActive
            });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Map(user);
    }

    private IQueryable<ApplicationUser> PublicCustomers() =>
        db.Users.AsNoTracking().Where(user =>
            db.UserRoles.Count(link => link.UserId == user.Id) == 1 &&
            db.UserRoles.Any(link =>
                link.UserId == user.Id &&
                db.Roles.Any(role =>
                    role.Id == link.RoleId &&
                    role.Name == RoleNames.PublicCustomer)));

    private static PublicCustomerAccountResponse Map(
        ApplicationUser user) => new(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.IsActive,
            user.CreatedAtUtc,
            user.LastLoginAtUtc,
            user.ConcurrencyStamp ?? string.Empty);
}
