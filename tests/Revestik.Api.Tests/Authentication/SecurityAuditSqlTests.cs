using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Services.Audit;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.Authentication;

public sealed class SecurityAuditSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    [Fact]
    public async Task AuditEntry_IsPersistedWithExplicitWhitelistedDetails()
    {
        var entityId = Guid.NewGuid().ToString("N");
        await using (var db = fixture.CreateDbContext())
        {
            SecurityAudit.Record(db, "UserUpdated", "User", entityId, "admin",
                new { previousRole = "Sales", newRole = "Accountant" });
            await db.SaveChangesAsync();
        }
        await using var verify = fixture.CreateDbContext();
        var entry = await verify.Set<SecurityAuditEntry>().AsNoTracking()
            .SingleAsync(x => x.EntityId == entityId);
        Assert.Equal("UserUpdated", entry.Action);
        Assert.Equal("admin", entry.ActorUserId);
        Assert.Contains("Accountant", entry.Details);
    }

    [Fact]
    public async Task AuditEntry_RollsBackWithItsTransaction()
    {
        var entityId = Guid.NewGuid().ToString("N");
        await using (var db = fixture.CreateDbContext())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            SecurityAudit.Record(db, "UserCreated", "User", entityId, "admin");
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }
        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.Set<SecurityAuditEntry>()
            .AnyAsync(x => x.EntityId == entityId));
    }
}
