using Microsoft.EntityFrameworkCore;
using Revestik.Api.Authorization;
using Revestik.Api.Data;

namespace Revestik.Api.Endpoints;

public static class SecurityAuditEndpoints
{
    public static IEndpointRouteBuilder MapSecurityAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/settings/audit", async (
            RevestikDbContext db, DateTime? fromUtc, DateTime? toUtc,
            string? actorId, string? action, string? entityType, int? page, int? pageSize,
            CancellationToken ct) =>
        {
            var number = page ?? 1;
            var size = pageSize ?? 25;
            if (number < 1 || number > 20000 || size < 1 || size > 100 ||
                (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc) ||
                actorId?.Length > 450 || action?.Length > 80 || entityType?.Length > 80)
                return Results.BadRequest(new { message = "Filtros o paginación inválidos." });

            var query = db.SecurityAuditEntries.AsNoTracking().AsQueryable();
            if (fromUtc.HasValue) query = query.Where(x => x.OccurredAtUtc >= fromUtc.Value);
            if (toUtc.HasValue) query = query.Where(x => x.OccurredAtUtc <= toUtc.Value);
            if (!string.IsNullOrWhiteSpace(actorId)) query = query.Where(x => x.ActorUserId == actorId);
            if (!string.IsNullOrWhiteSpace(action)) query = query.Where(x => x.Action == action);
            if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(x => x.EntityType == entityType);
            var total = await query.CountAsync(ct);
            var items = await query.OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id)
                .Skip((number - 1) * size).Take(size)
                .Select(x => new { x.Id, x.OccurredAtUtc, x.ActorUserId, x.Action,
                    x.EntityType, x.EntityId, x.Details }).ToListAsync(ct);
            return Results.Ok(new { total, page = number, pageSize = size, items });
        }).RequireAuthorization(PolicyNames.ViewAuditLog).WithTags("Security Audit")
            .WithName("GetSecurityAudit");
        return endpoints;
    }
}
