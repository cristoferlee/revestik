using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Projects;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Customers;
using Revestik.Shared.Projects;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectServiceSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string OwnerId = "projects-owner";
    private const string OtherId = "projects-other";

    private static ProjectUpsertRequest Request(string name = "Casa 1", decimal? amount = null) =>
        new(name, "Obra residencial", null, null, null, null, null, null,
            amount, amount is null ? null : "CRC",
            [new ProjectContactInput("Encargado", "Contratista", "88888888", "encargado@example.com")]);

    private async Task<int> SeedAsync(bool isActive = true)
    {
        await using var db = fixture.CreateDbContext();
        foreach (var id in new[] { OwnerId, OtherId })
            if (!await db.Users.AnyAsync(u => u.Id == id))
                db.Users.Add(new ApplicationUser
                {
                    Id = id, UserName = id, NormalizedUserName = id.ToUpperInvariant(),
                    Email = $"{id}@example.com", NormalizedEmail = $"{id}@example.com".ToUpperInvariant(),
                    DisplayName = id, SecurityStamp = Guid.NewGuid().ToString()
                });
        var customer = new Customer
        {
            Name = "Cliente Projects", IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9],
            Email = "cliente@example.com", PhoneNumber = "88888888",
            ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
            OtherSigns = "Heredia", IsActive = isActive, CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer.Id;
    }

    [Fact]
    public async Task Create_PersistsOwnerPlanningContactsAndOptionalBudget()
    {
        var customerId = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var created = await new ProjectService(db).CreateAsync(customerId, Request(), OwnerId, default);
        Assert.Equal(OwnerId, created.ResponsibleUserId);
        Assert.Equal(ProjectStatus.Planning, created.Status);
        Assert.Null(created.AgreedAmount);
        Assert.Single(created.Contacts);
        await using var verify = fixture.CreateDbContext();
        var persisted = await verify.Set<Project>().Include(p => p.Contacts).SingleAsync(p => p.Id == created.Id);
        Assert.Equal(customerId, persisted.CustomerId);
        Assert.Single(persisted.Contacts);
    }

    [Fact]
    public async Task Create_InactiveCustomer_IsRejected()
    {
        var customerId = await SeedAsync(false);
        await using var db = fixture.CreateDbContext();
        var error = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            new ProjectService(db).CreateAsync(customerId, Request(), OwnerId, default));
        Assert.Equal(ProjectFailureReason.InactiveCustomer, error.Reason);
    }

    [Fact]
    public async Task Create_DuplicateNameIgnoringCase_ReturnsConflictReason()
    {
        var customerId = await SeedAsync();
        await using (var db = fixture.CreateDbContext())
            await new ProjectService(db).CreateAsync(customerId, Request("Casa 1"), OwnerId, default);
        await using var otherContext = fixture.CreateDbContext();
        var error = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            new ProjectService(otherContext).CreateAsync(customerId, Request("casa 1"), OwnerId, default));
        Assert.Equal(ProjectFailureReason.Duplicate, error.Reason);
    }

    [Fact]
    public async Task Create_SameNameForDifferentCustomers_IsAllowed()
    {
        var firstCustomerId = await SeedAsync();
        var secondCustomerId = await SeedAsync();
        await using var first = fixture.CreateDbContext();
        var firstProject = await new ProjectService(first).CreateAsync(
            firstCustomerId, Request("Casa Norte"), OwnerId, default);
        await using var second = fixture.CreateDbContext();
        var secondProject = await new ProjectService(second).CreateAsync(
            secondCustomerId, Request("casa norte"), OwnerId, default);

        Assert.NotEqual(firstProject.Id, secondProject.Id);
        Assert.Equal(firstCustomerId, firstProject.CustomerId);
        Assert.Equal(secondCustomerId, secondProject.CustomerId);
    }

    [Fact]
    public async Task Create_NameWithSurroundingSpaces_ConflictsWithTrimmedName()
    {
        var customerId = await SeedAsync();
        await using (var first = fixture.CreateDbContext())
            await new ProjectService(first).CreateAsync(
                customerId, Request("Casa Norte"), OwnerId, default);

        await using var second = fixture.CreateDbContext();
        var error = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            new ProjectService(second).CreateAsync(
                customerId, Request("  casa norte  "), OwnerId, default));
        Assert.Equal(ProjectFailureReason.Duplicate, error.Reason);
    }

    [Fact]
    public async Task Update_NonOwnerForbidden_AdminAllowed()
    {
        var customerId = await SeedAsync();
        int projectId;
        await using (var db = fixture.CreateDbContext())
            projectId = (await new ProjectService(db).CreateAsync(customerId, Request(), OwnerId, default)).Id;
        await using (var db = fixture.CreateDbContext())
        {
            var error = await Assert.ThrowsAsync<ProjectOperationException>(() =>
                new ProjectService(db).UpdateAsync(projectId, (Request("Modificado") with { Version = 1 }), OtherId, false, default));
            Assert.Equal(ProjectFailureReason.Forbidden, error.Reason);
        }
        await using (var db = fixture.CreateDbContext())
        {
            var updated = await new ProjectService(db).UpdateAsync(projectId, (Request("Modificado") with { Version = 1 }), OtherId, true, default);
            Assert.Equal("Modificado", updated?.Name);
        }
    }

    [Fact]
    public async Task Delete_RemovesOwnContactsButNotCustomer()
    {
        var customerId = await SeedAsync();
        int projectId;
        await using (var db = fixture.CreateDbContext())
            projectId = (await new ProjectService(db).CreateAsync(customerId, Request(), OwnerId, default)).Id;
        await using (var db = fixture.CreateDbContext())
            Assert.True(await new ProjectService(db).DeleteAsync(projectId, OwnerId, false, 1, default));
        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.Set<Project>().AnyAsync(p => p.Id == projectId));
        Assert.False(await verify.Set<ProjectContact>().AnyAsync(c => c.ProjectId == projectId));
        Assert.True(await verify.Customers.AnyAsync(c => c.Id == customerId));
    }

    [Fact]
    public async Task Database_RejectsNegativeAgreedAmount()
    {
        var customerId = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        db.Set<Project>().Add(new Project
        {
            CustomerId = customerId, ResponsibleUserId = OwnerId,
            Name = "Presupuesto inválido", NormalizedName = "PRESUPUESTO INVÁLIDO",
            Description = "Prueba de restricción", AgreedAmount = -1, CurrencyCode = "CRC"
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task Update_TwoStaleContexts_SecondWriteRejected()
    {
        var customerId = await SeedAsync();
        int projectId;
        await using (var setup = fixture.CreateDbContext())
            projectId = (await new ProjectService(setup).CreateAsync(customerId, Request(), OwnerId, default)).Id;

        await using var first = fixture.CreateDbContext();
        await using var second = fixture.CreateDbContext();
        var serviceA = new ProjectService(first);
        var serviceB = new ProjectService(second);
        var snapshotA = await first.Set<Project>().SingleAsync(p => p.Id == projectId);
        var snapshotB = await second.Set<Project>().SingleAsync(p => p.Id == projectId);
        Assert.Equal(1, snapshotA.Version);
        Assert.Equal(1, snapshotB.Version);

        var changed = await serviceA.UpdateAsync(projectId,
            (Request("Primera edición") with { Version = 1 }), OwnerId, false, default);
        Assert.Equal(2, changed!.Version);

        var error = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            serviceB.UpdateAsync(projectId,
                (Request("Segunda edición") with { Version = 1 }), OwnerId, false, default));
        Assert.Equal(ProjectFailureReason.ConcurrencyConflict, error.Reason);

        await using var verification = fixture.CreateDbContext();
        var persisted = await verification.Set<Project>().SingleAsync(p => p.Id == projectId);
        Assert.Equal("Primera edición", persisted.Name);
        Assert.Equal(2, persisted.Version);
    }

    [Fact]
    public async Task Reassign_InvalidatesPreviouslyReadVersion()
    {
        var customerId = await SeedAsync();
        int projectId;
        await using (var setup = fixture.CreateDbContext())
            projectId = (await new ProjectService(setup).CreateAsync(customerId, Request(), OwnerId, default)).Id;
        await using (var admin = fixture.CreateDbContext())
        {
            var reassigned = await new ProjectService(admin).ReassignAsync(projectId, OtherId, 1, default);
            Assert.Equal(2, reassigned!.Version);
            Assert.Equal(OtherId, reassigned.ResponsibleUserId);
        }
        await using var stale = fixture.CreateDbContext();
        var error = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            new ProjectService(stale).UpdateAsync(projectId,
                (Request("Conflicto") with { Version = 1 }), OtherId, false, default));
        Assert.Equal(ProjectFailureReason.ConcurrencyConflict, error.Reason);
    }

    [Fact]
    public async Task Delete_RejectsStaleVersion()
    {
        var customerId = await SeedAsync();
        int projectId;
        await using (var setup = fixture.CreateDbContext())
            projectId = (await new ProjectService(setup).CreateAsync(customerId, Request(), OwnerId, default)).Id;
        await using (var change = fixture.CreateDbContext())
            await new ProjectService(change).UpdateAsync(projectId,
                (Request("Actualizado") with { Version = 1 }), OwnerId, false, default);
        await using var stale = fixture.CreateDbContext();
        var error = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            new ProjectService(stale).DeleteAsync(projectId, OwnerId, false, 1, default));
        Assert.Equal(ProjectFailureReason.ConcurrencyConflict, error.Reason);
        Assert.True(await stale.Set<Project>().AnyAsync(p => p.Id == projectId));
    }

}
