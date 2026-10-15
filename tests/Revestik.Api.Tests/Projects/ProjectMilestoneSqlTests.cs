
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Projects;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Customers;
using Revestik.Shared.Projects;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectMilestoneSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string Owner = "b03-sql-owner";

    private async Task<int> SeedAsync()
    {
        await using var db = fixture.CreateDbContext();

        if (!await db.Users.AnyAsync(x => x.Id == Owner))
        {
            db.Users.Add(new ApplicationUser
            {
                Id = Owner,
                UserName = Owner,
                NormalizedUserName = Owner.ToUpperInvariant(),
                Email = "b03-sql-owner@example.com",
                NormalizedEmail = "B03-SQL-OWNER@EXAMPLE.COM",
                DisplayName = "Owner",
                SecurityStamp = Guid.NewGuid().ToString()
            });
        }

        var customer = new Customer
        {
            Name = "Cliente hitos",
            IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9],
            Email = "b03@example.com",
            PhoneNumber = "88888888",
            ProvinceCode = "4",
            CantonCode = "01",
            DistrictCode = "01",
            OtherSigns = "Heredia",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var unique = Guid.NewGuid().ToString("N");

        var project = new Project
        {
            CustomerId = customer.Id,
            ResponsibleUserId = Owner,
            Name = "Proyecto " + unique,
            NormalizedName = unique.ToUpperInvariant()
        };

        db.Set<Project>().Add(project);
        await db.SaveChangesAsync();

        return project.Id;
    }

    private static MilestoneInput[] TwoMilestones() =>
    [
        new(null, "Medición", null, 30m, 0),
        new(null, "Instalación", null, 70m, 1)
    ];

    [Fact]
    public async Task UnconfiguredProject_HasNoProgress()
    {
        var projectId = await SeedAsync();

        await using var db = fixture.CreateDbContext();

        var result = await new ProjectMilestoneService(db)
            .GetAsync(projectId, default);

        Assert.NotNull(result);
        Assert.False(result.IsConfigured);
        Assert.Null(result.Percent);
        Assert.Empty(result.Milestones);
    }

    [Fact]
    public async Task ConfigureActivateCompleteAndReload_PreservesWeightedProgress()
    {
        var projectId = await SeedAsync();

        int milestoneId;
        long planVersion;
        long milestoneVersion;

        await using (var db = fixture.CreateDbContext())
        {
            var service = new ProjectMilestoneService(db);

            var draft = await service.ConfigureAsync(
                projectId, TwoMilestones(), null, Owner, false, default);

            Assert.Null(draft.Percent);
            Assert.NotNull(draft.PlanVersion);

            var active = await service.ActivateAsync(
                projectId, draft.PlanVersion.Value, Owner, false, default);

            Assert.Equal(0m, active.Percent);

            var first = Assert.Single(
                active.Milestones,
                x => x.Name == "Medición");

            milestoneId = first.Id;
            milestoneVersion = first.Version;
            planVersion = Assert.IsType<long>(active.PlanVersion);
        }

        await using (var db = fixture.CreateDbContext())
        {
            var result = await new ProjectMilestoneService(db)
                .SetCompletedAsync(
                    projectId,
                    milestoneId,
                    true,
                    planVersion,
                    milestoneVersion,
                    Owner,
                    false,
                    null,
                    default);

            Assert.Equal(30m, result.Percent);
        }

        await using var verify = fixture.CreateDbContext();

        var reloaded = await new ProjectMilestoneService(verify)
            .GetAsync(projectId, default);

        Assert.NotNull(reloaded);
        Assert.Equal(30m, reloaded.Percent);
    }

    [Fact]
    public async Task IncompleteWeights_CannotActivate()
    {
        var id = await SeedAsync();

        await using (var db = fixture.CreateDbContext())
        {
            var service = new ProjectMilestoneService(db);

            var draft = await service.ConfigureAsync(
                id,
                [new(null, "Entrega", null, 60m, 0)],
                null,
                Owner,
                false,
                default);

            Assert.NotNull(draft.PlanVersion);

            var ex = await Assert.ThrowsAsync<ProjectOperationException>(
                () => service.ActivateAsync(
                    id, draft.PlanVersion.Value, Owner, false, default));

            Assert.Equal(ProjectFailureReason.Invalid, ex.Reason);
        }

        await using var verify = fixture.CreateDbContext();

        var state = await new ProjectMilestoneService(verify)
            .GetAsync(id, default);

        Assert.NotNull(state);
        Assert.False(state.IsActive);
        Assert.Null(state.Percent);
    }

    [Fact]
    public async Task Reversal_RecalculatesAndPreservesBothRevisions()
    {
        var id = await SeedAsync();
        int milestoneId;

        await using (var db = fixture.CreateDbContext())
        {
            var service = new ProjectMilestoneService(db);

            var draft = await service.ConfigureAsync(
                id, TwoMilestones(), null, Owner, false, default);

            Assert.NotNull(draft.PlanVersion);

            var active = await service.ActivateAsync(
                id, draft.PlanVersion.Value, Owner, false, default);

            var item = Assert.Single(
                active.Milestones,
                x => x.Name == "Medición");

            milestoneId = item.Id;

            var done = await service.SetCompletedAsync(
                id,
                milestoneId,
                true,
                Assert.IsType<long>(active.PlanVersion),
                item.Version,
                Owner,
                false,
                null,
                default);

            Assert.Equal(30m, done.Percent);

            var updatedMilestone = Assert.Single(
                done.Milestones,
                x => x.Id == milestoneId);

            var reverted = await service.SetCompletedAsync(
                id,
                milestoneId,
                false,
                Assert.IsType<long>(done.PlanVersion),
                updatedMilestone.Version,
                Owner,
                false,
                "Corrección",
                default);

            Assert.Equal(0m, reverted.Percent);
        }

        await using var verify = fixture.CreateDbContext();

        var revisions = await verify
            .Set<ProjectMilestoneRevision>()
            .AsNoTracking()
            .Where(x => x.MilestoneId == milestoneId)
            .OrderBy(x => x.PreviousVersion)
            .ToListAsync();

        Assert.Equal(2, revisions.Count);
        Assert.Equal("Completed", revisions[0].ChangeKind);
        Assert.Equal("Reverted", revisions[1].ChangeKind);
        Assert.Equal("Corrección", revisions[1].Reason);

        var milestone = await verify
            .Set<ProjectMilestone>()
            .AsNoTracking()
            .SingleAsync(x => x.Id == milestoneId);

        Assert.False(milestone.IsCompleted);
        Assert.Null(milestone.CompletedAtUtc);
    }

    [Fact]
    public async Task DuplicateNamesAndPositions_AreRejected()
    {
        var id = await SeedAsync();

        await using (var db = fixture.CreateDbContext())
        {
            var service = new ProjectMilestoneService(db);

            var duplicateName =
                await Assert.ThrowsAsync<ProjectOperationException>(
                    () => service.ConfigureAsync(
                        id,
                        [
                            new(null, "Medición", null, 40m, 0),
                            new(null, " medición ", null, 60m, 1)
                        ],
                        null,
                        Owner,
                        false,
                        default));

            Assert.Equal(
                ProjectFailureReason.Invalid,
                duplicateName.Reason);

            var duplicateOrder =
                await Assert.ThrowsAsync<ProjectOperationException>(
                    () => service.ConfigureAsync(
                        id,
                        [
                            new(null, "Medición", null, 40m, 0),
                            new(null, "Entrega", null, 60m, 0)
                        ],
                        null,
                        Owner,
                        false,
                        default));

            Assert.Equal(
                ProjectFailureReason.Invalid,
                duplicateOrder.Reason);
        }

        await using var verify = fixture.CreateDbContext();

        Assert.False(await verify
            .Set<ProjectMilestonePlan>()
            .AnyAsync(x => x.ProjectId == id));
    }

    [Fact]
    public async Task NonOwnerAndStalePlanVersion_AreRejected()
    {
        var id = await SeedAsync();
        long oldVersion;

        await using (var db = fixture.CreateDbContext())
        {
            var service = new ProjectMilestoneService(db);

            var denied =
                await Assert.ThrowsAsync<ProjectOperationException>(
                    () => service.ConfigureAsync(
                        id,
                        TwoMilestones(),
                        null,
                        "not-owner",
                        false,
                        default));

            Assert.Equal(
                ProjectFailureReason.Forbidden,
                denied.Reason);
        }

        await using (var db = fixture.CreateDbContext())
        {
            var service = new ProjectMilestoneService(db);

            var draft = await service.ConfigureAsync(
                id, TwoMilestones(), null, Owner, false, default);

            oldVersion = Assert.IsType<long>(draft.PlanVersion);

            await service.ActivateAsync(
                id, oldVersion, Owner, false, default);
        }

        await using var check = fixture.CreateDbContext();

        var conflict =
            await Assert.ThrowsAsync<ProjectOperationException>(
                () => new ProjectMilestoneService(check)
                    .ActivateAsync(
                        id, oldVersion, Owner, false, default));

        Assert.Equal(
            ProjectFailureReason.ConcurrencyConflict,
            conflict.Reason);
    }
}