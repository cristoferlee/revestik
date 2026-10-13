using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Services.Projects;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Projects;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectActivitySqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private async Task<(int projectId, int categoryId)> SeedAsync()
    {
        await using var db = fixture.CreateDbContext();
        var project = new Project { CustomerId = 0, ResponsibleUserId = "owner", Name = "Proyecto", NormalizedName = "PROYECTO" };
        // SQL foreign keys require a real Customer and AspNetUser; reuse existing B01 tests' creation pattern.
        var user = new Revestik.Api.Models.Identity.ApplicationUser
        {
            Id = "owner", UserName = "owner", NormalizedUserName = "OWNER", Email = "owner@example.com",
            NormalizedEmail = "OWNER@EXAMPLE.COM", DisplayName = "Owner", SecurityStamp = Guid.NewGuid().ToString()
        };
        if (!await db.Users.AnyAsync(x => x.Id == "owner")) db.Users.Add(user);
        var customer = new Customer
        {
            Name = "Cliente B02", IdentificationType = Revestik.Shared.Customers.IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9], Email = "b02@example.com",
            PhoneNumber = "88888888", ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
            OtherSigns = "Heredia", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        project.CustomerId = customer.Id;
        db.Set<Project>().Add(project);
        var category = new ProjectEventCategory { Name = "Seguimiento " + Guid.NewGuid().ToString("N")[..8], NormalizedName = Guid.NewGuid().ToString("N") };
        db.Set<ProjectEventCategory>().Add(category);
        await db.SaveChangesAsync();
        return (project.Id, category.Id);
    }

    [Fact]
    public async Task CreateAndList_OrdersByOccurrenceDate()
    {
        var (projectId, categoryId) = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var service = new ProjectActivityService(db);
        await service.CreateAsync(projectId, new(categoryId, "Primero", null, new DateOnly(2026, 1, 5)), "owner", false, default);
        await service.CreateAsync(projectId, new(categoryId, "Segundo", null, new DateOnly(2026, 2, 5)), "owner", false, default);
        var events = await service.ListAsync(projectId, new(), default);
        Assert.Equal(new[] { "Segundo", "Primero" }, events!.Select(x => x.Title));
    }

    [Fact]
    public async Task UpdateAndVoid_PreservePreviousVersions()
    {
        var (projectId, categoryId) = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var service = new ProjectActivityService(db);
        var created = await service.CreateAsync(projectId, new(categoryId, "Original", "Detalle", new DateOnly(2026, 1, 5)), "owner", false, default);
        var changed = await service.UpdateAsync(projectId, created.Id,
            new(categoryId, "Modificado", "Detalle", created.OccurredOn, created.Version), "owner", false, default);
        Assert.NotNull(changed);
        var voided = await service.VoidAsync(projectId, created.Id, new(changed!.Version, "Registro incorrecto"), "owner", false, default);
        Assert.True(voided!.IsVoided);
        await using var verify = fixture.CreateDbContext();
        var revisions = await verify.Set<ProjectEventRevision>().Where(x => x.ProjectEventId == created.Id)
            .OrderBy(x => x.PreviousVersion).ToListAsync();
        Assert.Equal(2, revisions.Count);
        Assert.Equal("Original", revisions[0].Title);
        Assert.Equal("Modificado", revisions[1].Title);
    }

    [Fact]
    public async Task NonOwner_CannotCreateEvents()
    {
        var (projectId, categoryId) = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var failure = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            new ProjectActivityService(db).CreateAsync(projectId,
                new(categoryId, "No permitido", null, DateOnly.FromDateTime(DateTime.Today)), "other", false, default));
        Assert.Equal(ProjectFailureReason.Forbidden, failure.Reason);
    }
}
