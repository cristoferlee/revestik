using System.Text.Json;
using Revestik.Api.Data;
using Revestik.Api.Models;

namespace Revestik.Api.Services.Audit;

public static class SecurityAudit
{
    // The caller must save with its business change, in the same DbContext/transaction.
    public static void Record(RevestikDbContext db, string action, string entityType,
        string? entityId, string? actorId, object? details = null)
    {
        var json = details is null ? null : JsonSerializer.Serialize(details);
        if (json?.Length > 2000) throw new InvalidOperationException("Audit details exceed size limit.");
        db.Set<SecurityAuditEntry>().Add(new SecurityAuditEntry
        {
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserId = actorId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = json
        });
    }
}
