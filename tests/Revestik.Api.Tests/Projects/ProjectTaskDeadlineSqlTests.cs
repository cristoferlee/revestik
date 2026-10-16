using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Projects;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Customers;
using Revestik.Shared.Projects;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectTaskDeadlineSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string Owner = "b06-owner";
    private async Task<(int ProjectId, int EventId)> SeedAsync()
    {
        await using var db = fixture.CreateDbContext();
        if (!await db.Users.AnyAsync(x => x.Id == Owner))
            db.Users.Add(new ApplicationUser
            {
                Id = Owner, UserName = Owner, NormalizedUserName = Owner.ToUpperInvariant(),
                Email = "b06-owner@example.com", NormalizedEmail = "B06-OWNER@EXAMPLE.COM",
                DisplayName = Owner, SecurityStamp = Guid.NewGuid().ToString()
            });
        var customer = new Customer
        {
            Name = "Cliente B06", IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9], Email = "b06@example.com",
            PhoneNumber = "88888888", ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
            OtherSigns = "Heredia", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var project = new Project
        {
            CustomerId = customer.Id, ResponsibleUserId = Owner,
            Name = $"B06-{Guid.NewGuid():N}", NormalizedName = Guid.NewGuid().ToString("N").ToUpperInvariant()
        };
        var cat = new ProjectEventCategory
        {
            Name = $"B06-{Guid.NewGuid():N}", NormalizedName = Guid.NewGuid().ToString("N").ToUpperInvariant()
        };
        db.Set<Project>().Add(project);
        db.Set<ProjectEventCategory>().Add(cat);
        await db.SaveChangesAsync();
        var ev = new ProjectEvent
        {
            ProjectId = project.Id, CategoryId = cat.Id, Title = "Incidencia", Description = "Revisar",
            OccurredOn = new DateOnly(2026, 10, 16), CreatedByUserId = Owner,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Set<ProjectEvent>().Add(ev);
        await db.SaveChangesAsync();
        return (project.Id, ev.Id);
    }

    [Fact]
    public async Task Tasks_CreateUpdateAndHistory_ArePreserved()
    {
        var (projectId, _) = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var service = new ProjectTaskDeadlineService(db);
        var created = await service.CreateTaskAsync(projectId,
            new("Coordinar visita", "Pendiente", Owner, new DateOnly(2026, 10, 20)), Owner, false, default);
        Assert.Equal(1, created.Version);
        var updated = await service.UpdateTaskAsync(projectId, created.Id,
            new("Visita acordada", "Listo", Owner, new DateOnly(2026, 10, 21), true,
                created.Version, "Confirmado"), Owner, false, default);
        Assert.NotNull(updated);
        Assert.True(updated.IsCompleted);
        Assert.NotNull(updated.CompletedAtUtc);
        var previous = Assert.Single((await service.TaskHistoryAsync(projectId, created.Id, default))!);
        Assert.Equal("Coordinar visita", previous.Title);
        Assert.False(previous.WasCompleted);
        Assert.Equal(new DateOnly(2026, 10, 20), previous.DueOn);
        var error = await Assert.ThrowsAsync<ProjectOperationException>(() => service.UpdateTaskAsync(projectId, created.Id,
            new("Obsoleto", null, null, null, false, created.Version, "Cambio"), Owner, false, default));
        Assert.Equal(ProjectFailureReason.ConcurrencyConflict, error.Reason);
    }

    [Fact]
    public async Task NonOwnerCannotEdit_AndOtherProjectCannotReadTask()
    {
        var (projectA, _) = await SeedAsync();
        var (projectB, _) = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var service = new ProjectTaskDeadlineService(db);
        var created = await service.CreateTaskAsync(projectA, new("Visita", null, null, null), Owner, false, default);
        Assert.Null(await service.TaskAsync(projectB, created.Id, default));
        var error = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            service.CreateTaskAsync(projectA, new("Intrusión", null, null, null), "intruder", false, default));
        Assert.Equal(ProjectFailureReason.Forbidden, error.Reason);
    }

    [Fact]
    public async Task EventDeadline_ChangesPreserveHistoryAndRejectStaleVersion()
    {
        var (projectId, eventId) = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var service = new ProjectTaskDeadlineService(db);
        var first = await service.SetDeadlineAsync(projectId, eventId,
            new(new DateOnly(2026, 10, 24), null, "Inicial"), Owner, false, default);
        Assert.NotNull(first);
        var second = await service.SetDeadlineAsync(projectId, eventId,
            new(new DateOnly(2026, 10, 28), first.Version, "Reprogramación"), Owner, false, default);
        Assert.Equal(first.Version + 1, second!.Version);
        var previous = Assert.Single((await service.DeadlineHistoryAsync(projectId, eventId, default))!);
        Assert.Equal(new DateOnly(2026, 10, 24), previous.PreviousDueOn);
        var error = await Assert.ThrowsAsync<ProjectOperationException>(() => service.SetDeadlineAsync(projectId,
            eventId, new(null, first.Version, "Obsoleto"), Owner, false, default));
        Assert.Equal(ProjectFailureReason.ConcurrencyConflict, error.Reason);
    }

    [Fact]
    public async Task EventDeadline_VoidedEventRejected_AndProjectIsolation()
    {
        var (projectA, eventA) = await SeedAsync();
        var (projectB, _) = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var service = new ProjectTaskDeadlineService(db);
        Assert.Null(await service.GetDeadlineAsync(projectB, eventA, default));
        Assert.Null(await service.SetDeadlineAsync(projectB, eventA,
            new(new DateOnly(2026, 10, 29), null, "Intento"), Owner, false, default));
        var ev = await db.Set<ProjectEvent>().SingleAsync(x => x.Id == eventA);
        ev.IsVoided = true;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ProjectOperationException>(() => service.SetDeadlineAsync(projectA,
            eventA, new(new DateOnly(2026, 10, 29), null, "Intento"), Owner, false, default));
    }
}
