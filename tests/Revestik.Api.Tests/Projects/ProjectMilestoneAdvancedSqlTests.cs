using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Projects;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Customers;
using Revestik.Shared.Projects;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectMilestoneAdvancedSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string Owner = "b03-advanced-owner";

    private static MilestoneInput[] Initial() =>
    [
        new(null, "Medición", null, 30m, 0),
        new(null, "Instalación", null, 70m, 1)
    ];

    private async Task<int> SeedAsync()
    {
        await using var db = fixture.CreateDbContext();
        if (!await db.Users.AnyAsync(x => x.Id == Owner))
            db.Users.Add(new ApplicationUser
            {
                Id = Owner, UserName = Owner, NormalizedUserName = Owner.ToUpperInvariant(),
                Email = "b03-advanced-owner@example.com",
                NormalizedEmail = "B03-ADVANCED-OWNER@EXAMPLE.COM",
                DisplayName = "Owner", SecurityStamp = Guid.NewGuid().ToString()
            });
        var customer = new Customer
        {
            Name = "Cliente hitos avanzados", IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9],
            Email = "b03-advanced@example.com", PhoneNumber = "88888888",
            ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
            OtherSigns = "Heredia", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var unique = Guid.NewGuid().ToString("N");
        var project = new Project
        {
            CustomerId = customer.Id, ResponsibleUserId = Owner,
            Name = "Proyecto " + unique, NormalizedName = unique.ToUpperInvariant()
        };
        db.Set<Project>().Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    [Fact]
    public async Task ActivePlan_ValidReconfigurationRecalculatesAndKeepsHistory()
    {
        var projectId = await SeedAsync();
        int firstId;
        await using (var db = fixture.CreateDbContext())
        {
            var service = new ProjectMilestoneService(db);
            var draft = await service.ConfigureAsync(projectId, Initial(), null, Owner, false, default);
            var active = await service.ActivateAsync(projectId,
                Assert.IsType<long>(draft.PlanVersion), Owner, false, default);
            var first = Assert.Single(active.Milestones, x => x.Name == "Medición");
            firstId = first.Id;
            var completed = await service.SetCompletedAsync(projectId, first.Id, true,
                Assert.IsType<long>(active.PlanVersion), first.Version, Owner, false, null, default);
            var second = Assert.Single(completed.Milestones, x => x.Name == "Instalación");
            var updated = await service.ConfigureAsync(projectId,
            [
                new(first.Id, "Medición", null, 40m, 1),
                new(second.Id, "Instalación", null, 60m, 0)
            ], Assert.IsType<long>(completed.PlanVersion), Owner, false, default);
            Assert.Equal(40m, updated.Percent);
            Assert.Equal(1, Assert.Single(updated.Milestones, x => x.Id == firstId).SortOrder);
            Assert.Equal(60m, Assert.Single(updated.Milestones, x => x.Id == second.Id).Weight);
        }
        await using var verify = fixture.CreateDbContext();
        var reloaded = await new ProjectMilestoneService(verify).GetAsync(projectId, default);
        Assert.NotNull(reloaded);
        Assert.Equal(40m, reloaded.Percent);
        var revisions = await verify.Set<ProjectMilestoneRevision>().AsNoTracking()
            .Where(x => x.MilestoneId == firstId).ToListAsync();
        Assert.Equal(2, revisions.Count);
    }

    [Fact]
    public async Task ActivePlan_InvalidWeightChangeRollsBackEntireReconfiguration()
    {
        var projectId = await SeedAsync();
        long version;
        int firstId;
        await using (var db = fixture.CreateDbContext())
        {
            var service = new ProjectMilestoneService(db);
            var draft = await service.ConfigureAsync(projectId, Initial(), null, Owner, false, default);
            var active = await service.ActivateAsync(projectId,
                Assert.IsType<long>(draft.PlanVersion), Owner, false, default);
            version = Assert.IsType<long>(active.PlanVersion);
            var first = Assert.Single(active.Milestones, x => x.Name == "Medición");
            var second = Assert.Single(active.Milestones, x => x.Name == "Instalación");
            firstId = first.Id;
            var failure = await Assert.ThrowsAsync<ProjectOperationException>(() =>
                service.ConfigureAsync(projectId,
                [
                    new(first.Id, "Medición cambiada", null, 40m, 0),
                    new(second.Id, "Instalación", null, 70m, 1)
                ], version, Owner, false, default));
            Assert.Equal(ProjectFailureReason.Invalid, failure.Reason);
        }
        await using var verify = fixture.CreateDbContext();
        var state = await new ProjectMilestoneService(verify).GetAsync(projectId, default);
        Assert.NotNull(state);
        Assert.Equal(version, state.PlanVersion);
        Assert.Equal(0m, state.Percent);
        var unchanged = Assert.Single(state.Milestones, x => x.Id == firstId);
        Assert.Equal("Medición", unchanged.Name);
        Assert.Equal(30m, unchanged.Weight);
        Assert.Empty(await verify.Set<ProjectMilestoneRevision>().AsNoTracking()
            .Where(x => x.MilestoneId == firstId).ToListAsync());
    }

    [Fact]
    public async Task RepeatedCompletionAndConfiguration_DoNotCreateExtraRevisions()
    {
        var projectId = await SeedAsync();
        int milestoneId;
        long version;
        await using (var db = fixture.CreateDbContext())
        {
            var service = new ProjectMilestoneService(db);
            var draft = await service.ConfigureAsync(projectId, Initial(), null, Owner, false, default);
            var active = await service.ActivateAsync(projectId,
                Assert.IsType<long>(draft.PlanVersion), Owner, false, default);
            var first = Assert.Single(active.Milestones, x => x.Name == "Medición");
            milestoneId = first.Id;
            var completed = await service.SetCompletedAsync(projectId, first.Id, true,
                Assert.IsType<long>(active.PlanVersion), first.Version, Owner, false, null, default);
            version = Assert.IsType<long>(completed.PlanVersion);
            var completedMilestone = Assert.Single(completed.Milestones, x => x.Id == first.Id);
            var repeated = await service.SetCompletedAsync(projectId, first.Id, true,
                version, completedMilestone.Version, Owner, false, null, default);
            Assert.Equal(version, repeated.PlanVersion);
            Assert.Equal(30m, repeated.Percent);
            var second = Assert.Single(repeated.Milestones, x => x.Name == "Instalación");
            var unchanged = await service.ConfigureAsync(projectId,
            [
                new(first.Id, first.Name, first.Description, first.Weight, first.SortOrder),
                new(second.Id, second.Name, second.Description, second.Weight, second.SortOrder)
            ], version, Owner, false, default);
            Assert.Equal(version, unchanged.PlanVersion);
        }
        await using var verify = fixture.CreateDbContext();
        var count = await verify.Set<ProjectMilestoneRevision>()
            .CountAsync(x => x.MilestoneId == milestoneId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task TwoContexts_StalePlanAndMilestoneVersionsCannotOverwriteChange()
    {
        var projectId = await SeedAsync();
        int milestoneId;
        long oldPlanVersion;
        long oldMilestoneVersion;
        await using (var db = fixture.CreateDbContext())
        {
            var service = new ProjectMilestoneService(db);
            var draft = await service.ConfigureAsync(projectId, Initial(), null, Owner, false, default);
            var active = await service.ActivateAsync(projectId,
                Assert.IsType<long>(draft.PlanVersion), Owner, false, default);
            var first = Assert.Single(active.Milestones, x => x.Name == "Medición");
            milestoneId = first.Id;
            oldPlanVersion = Assert.IsType<long>(active.PlanVersion);
            oldMilestoneVersion = first.Version;
        }
        await using var firstContext = fixture.CreateDbContext();
        await using var secondContext = fixture.CreateDbContext();
        var firstService = new ProjectMilestoneService(firstContext);
        var secondService = new ProjectMilestoneService(secondContext);
        var firstResult = await firstService.SetCompletedAsync(projectId, milestoneId, true,
            oldPlanVersion, oldMilestoneVersion, Owner, false, null, default);
        Assert.Equal(30m, firstResult.Percent);
        var conflict = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            secondService.SetCompletedAsync(projectId, milestoneId, false,
                oldPlanVersion, oldMilestoneVersion, Owner, false, "Reversión obsoleta", default));
        Assert.Equal(ProjectFailureReason.ConcurrencyConflict, conflict.Reason);
        await using var verify = fixture.CreateDbContext();
        var state = await new ProjectMilestoneService(verify).GetAsync(projectId, default);
        Assert.NotNull(state);
        Assert.Equal(30m, state.Percent);
        Assert.True(Assert.Single(state.Milestones, x => x.Id == milestoneId).IsCompleted);
    }
}
