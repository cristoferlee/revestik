using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Projects;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Customers;
using Revestik.Shared.Projects;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectClosedStateSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string Owner = "b06-closed-state-owner";

    private async Task<(int ProjectId, int EventId)> SeedAsync(ProjectStatus status)
    {
        await using var db = fixture.CreateDbContext();
        if (!await db.Users.AnyAsync(x => x.Id == Owner))
            db.Users.Add(new ApplicationUser
            {
                Id = Owner, UserName = Owner, NormalizedUserName = Owner.ToUpperInvariant(),
                Email = "b06-closed-owner@example.com", NormalizedEmail = "B06-CLOSED-OWNER@EXAMPLE.COM",
                DisplayName = Owner, SecurityStamp = Guid.NewGuid().ToString()
            });
        var customer = new Customer
        {
            Name = "Cierre B06", IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9], Email = "b06-closed@example.com",
            PhoneNumber = "88888888", ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
            OtherSigns = "Heredia", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var name = Guid.NewGuid().ToString("N");
        var project = new Project
        {
            CustomerId = customer.Id, ResponsibleUserId = Owner, Name = name,
            NormalizedName = name.ToUpperInvariant(), Status = status
        };
        var categoryName = Guid.NewGuid().ToString("N");
        var category = new ProjectEventCategory
        {
            Name = categoryName, NormalizedName = categoryName.ToUpperInvariant()
        };
        db.Set<Project>().Add(project);
        db.Set<ProjectEventCategory>().Add(category);
        await db.SaveChangesAsync();
        var ev = new ProjectEvent
        {
            ProjectId = project.Id, CategoryId = category.Id, Title = "Visita",
            Description = "Registro histórico", OccurredOn = new DateOnly(2026, 10, 15),
            CreatedByUserId = Owner, CreatedAtUtc = DateTime.UtcNow
        };
        db.Set<ProjectEvent>().Add(ev);
        await db.SaveChangesAsync();
        return (project.Id, ev.Id);
    }

    [Theory]
    [InlineData(ProjectStatus.Completed)]
    [InlineData(ProjectStatus.Cancelled)]
    [InlineData(ProjectStatus.Archived)]
    public async Task ClosedProject_RejectsOperationalWrites_ButPreservesReads(ProjectStatus status)
    {
        var (projectId, eventId) = await SeedAsync(status);
        await using var db = fixture.CreateDbContext();
        var tasks = new ProjectTaskDeadlineService(db);
        var activities = new ProjectActivityService(db);
        var details = new ProjectEventDetailService(db);

        var taskError = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            tasks.CreateTaskAsync(projectId, new("Nueva tarea", null, null, null), Owner, false, default));
        Assert.Equal(ProjectFailureReason.Invalid, taskError.Reason);

        var deadlineError = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            tasks.SetDeadlineAsync(projectId, eventId,
                new(new DateOnly(2026, 10, 25), null, "Nuevo plazo"), Owner, false, default));
        Assert.Equal(ProjectFailureReason.Invalid, deadlineError.Reason);

        var eventError = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            activities.CreateAsync(projectId,
                new(1, "Nueva actividad", null, new DateOnly(2026, 10, 16)), Owner, false, default));
        Assert.Equal(ProjectFailureReason.Invalid, eventError.Reason);

        var commentError = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            details.AddCommentAsync(projectId, eventId, new("Seguimiento"), Owner, false, default));
        Assert.Equal(ProjectFailureReason.Invalid, commentError.Reason);

        var agreementError = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            details.SaveAgreementAsync(projectId, eventId,
                new(ProjectAgreementStatus.Pending, null, null, null, null), Owner, false, default));
        Assert.Equal(ProjectFailureReason.Invalid, agreementError.Reason);

        Assert.NotNull(await activities.GetAsync(projectId, eventId, default));
        Assert.Empty((await tasks.TasksAsync(projectId, default))!);
        Assert.Empty((await details.CommentsAsync(projectId, eventId, default))!);
    }

    [Fact]
    public async Task PausedProject_DoesNotInheritClosedStateWriteRestriction()
    {
        var (projectId, _) = await SeedAsync(ProjectStatus.Paused);
        await using var db = fixture.CreateDbContext();
        var task = await new ProjectTaskDeadlineService(db).CreateTaskAsync(projectId,
            new("Planificar visita", null, null, new DateOnly(2026, 10, 25)), Owner, false, default);
        Assert.True(task.Id > 0);
    }
}
