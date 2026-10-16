using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Projects;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Customers;
using Revestik.Shared.Projects;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectLifecycleSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string Owner = "b06c-sql-owner";
    private const string Other = "b06c-sql-other";

    private async Task<int> SeedAsync()
    {
        await using var db = fixture.CreateDbContext();
        foreach (var id in new[] { Owner, Other })
            if (!await db.Users.AnyAsync(x => x.Id == id))
                db.Users.Add(new ApplicationUser
                {
                    Id = id, UserName = id, NormalizedUserName = id.ToUpperInvariant(),
                    Email = $"{id}@example.com", NormalizedEmail = $"{id}@example.com".ToUpperInvariant(),
                    DisplayName = id, SecurityStamp = Guid.NewGuid().ToString()
                });
        var customer = new Customer
        {
            Name = "B06C SQL", IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9],
            Email = "b06c-sql@example.com", PhoneNumber = "88888888",
            ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
            OtherSigns = "Heredia", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var name = Guid.NewGuid().ToString("N");
        var project = new Project
        {
            CustomerId = customer.Id, ResponsibleUserId = Owner,
            Name = name, NormalizedName = name.ToUpperInvariant()
        };
        db.Set<Project>().Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    [Fact]
    public async Task TransitionCloseReopen_PreservesOrderedAuditAndProjectVersion()
    {
        var id = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var service = new ProjectLifecycleService(db);
        var active = await service.TransitionAsync(id,
            new(ProjectStatus.Active, 1, "Inicio manual"), Owner, false, default);
        Assert.Equal(ProjectStatus.Active, active!.Status);
        Assert.Equal(2, active.Version);
        var closed = await service.CloseAsync(id,
            new(new DateOnly(2026, 10, 16), "Entrega aprobada", null, active.Version), Owner, false, default);
        Assert.Equal(ProjectStatus.Completed, closed!.Status);
        var reopened = await service.ReopenAsync(id,
            new(ProjectStatus.Active, closed.Version, "Garantía"), Owner, false, default);
        Assert.Equal(ProjectStatus.Active, reopened!.Status);
        var history = await service.HistoryAsync(id, default);
        Assert.NotNull(history);
        Assert.Equal(3, history.Count);
        Assert.Equal(new[] { "Transition", "Close", "Reopen" },
            history.OrderBy(x => x.PreviousProjectVersion).Select(x => x.Action));
        var close = Assert.Single(history, x => x.Action == "Close");
        Assert.Equal(new DateOnly(2026, 10, 16), close.ClosedOn);
        Assert.Equal("Entrega aprobada", close.ClosingObservations);
        Assert.All(history, x => Assert.Equal(Owner, x.ActorUserId));
    }

    [Fact]
    public async Task StaleVersionAndNonOwner_AreRejectedWithoutNewHistory()
    {
        var id = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var service = new ProjectLifecycleService(db);
        var forbidden = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            service.TransitionAsync(id, new(ProjectStatus.Active, 1, null), Other, false, default));
        Assert.Equal(ProjectFailureReason.Forbidden, forbidden.Reason);
        await service.TransitionAsync(id, new(ProjectStatus.Active, 1, null), Owner, false, default);
        var stale = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            service.CloseAsync(id, new(new DateOnly(2026, 10, 16), null, null, 1), Owner, false, default));
        Assert.Equal(ProjectFailureReason.ConcurrencyConflict, stale.Reason);
        Assert.Single((await service.HistoryAsync(id, default))!);
    }

    [Fact]
    public async Task EvidenceFromOtherProject_IsRejectedAndRollbackPreservesState()
    {
        var first = await SeedAsync();
        var second = await SeedAsync();
        int evidence;
        await using (var db = fixture.CreateDbContext())
        {
            var document = new ProjectDocument
            {
                ProjectId = second, Category = "Other", DisplayName = "Acta",
                CreatedByUserId = Owner, CreatedAtUtc = DateTime.UtcNow
            };
            db.Set<ProjectDocument>().Add(document);
            await db.SaveChangesAsync();
            evidence = document.Id;
        }
        await using var check = fixture.CreateDbContext();
        var service = new ProjectLifecycleService(check);
        var error = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            service.CloseAsync(first, new(new DateOnly(2026, 10, 16), "Cierre", evidence, 1), Owner, false, default));
        Assert.Equal(ProjectFailureReason.Invalid, error.Reason);
        Assert.Empty((await service.HistoryAsync(first, default))!);
        Assert.Equal(ProjectStatus.Planning,
            (await check.Set<Project>().AsNoTracking().SingleAsync(x => x.Id == first)).Status);
    }

    [Fact]
    public async Task DatabaseForeignKey_RejectsCrossProjectEvidenceEvenWithoutService()
    {
        var first = await SeedAsync();
        var second = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var document = new ProjectDocument
        {
            ProjectId = second, Category = "Other", DisplayName = "Acta",
            CreatedByUserId = Owner, CreatedAtUtc = DateTime.UtcNow
        };
        db.Set<ProjectDocument>().Add(document);
        await db.SaveChangesAsync();
        db.Set<ProjectLifecycleChange>().Add(new ProjectLifecycleChange
        {
            ProjectId = first, PreviousProjectVersion = 1,
            PreviousStatus = ProjectStatus.Planning, NewStatus = ProjectStatus.Completed,
            Action = "Close", ClosedOn = new DateOnly(2026, 10, 16),
            EvidenceDocumentId = document.Id, ActorUserId = Owner, ChangedAtUtc = DateTime.UtcNow
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
