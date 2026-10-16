using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Projects;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Customers;
using Revestik.Shared.Projects;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectDashboardAlertSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string Owner = "b06-dashboard-owner";
    private static readonly DateOnly Today = new(2026, 10, 16);

    private async Task<(int ProjectId, int EventId)> SeedAsync(ProjectStatus status = ProjectStatus.Active,
        bool voided = false, string? agreementStatus = null)
    {
        await using var db = fixture.CreateDbContext();
        if (!await db.Users.AnyAsync(x => x.Id == Owner))
            db.Users.Add(new ApplicationUser
            {
                Id = Owner, UserName = Owner, NormalizedUserName = Owner.ToUpperInvariant(),
                Email = "b06-dashboard@example.com", NormalizedEmail = "B06-DASHBOARD@EXAMPLE.COM",
                DisplayName = Owner, SecurityStamp = Guid.NewGuid().ToString()
            });
        var customer = new Customer
        {
            Name = "B06 Dashboard", IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9],
            Email = "dashboard@example.com", PhoneNumber = "88888888",
            ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
            OtherSigns = "Heredia", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var name = Guid.NewGuid().ToString("N");
        var project = new Project
        {
            CustomerId = customer.Id, ResponsibleUserId = Owner,
            Name = name, NormalizedName = name.ToUpperInvariant(), Status = status
        };
        var categoryName = Guid.NewGuid().ToString("N");
        var category = new ProjectEventCategory { Name = categoryName, NormalizedName = categoryName.ToUpperInvariant() };
        db.Set<Project>().Add(project);
        db.Set<ProjectEventCategory>().Add(category);
        await db.SaveChangesAsync();
        var ev = new ProjectEvent
        {
            ProjectId = project.Id, CategoryId = category.Id, Title = "Revisar incidencia",
            Description = "Pendiente", OccurredOn = Today,
            CreatedByUserId = Owner, CreatedAtUtc = DateTime.UtcNow, IsVoided = voided
        };
        db.Set<ProjectEvent>().Add(ev);
        await db.SaveChangesAsync();
        if (agreementStatus is not null)
        {
            db.Set<ProjectEventAgreement>().Add(new ProjectEventAgreement
            {
                ProjectEventId = ev.Id, Status = agreementStatus,
                UpdatedAtUtc = DateTime.UtcNow, UpdatedByUserId = Owner
            });
            await db.SaveChangesAsync();
        }
        return (project.Id, ev.Id);
    }

    [Fact]
    public async Task Tasks_BoundariesCompletionAndOrder()
    {
        var (id, _) = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        foreach (var (name, due, completed) in new[]
        {
            ("Overdue", Today.AddDays(-2), false), ("Today", Today, false),
            ("DaySeven", Today.AddDays(7), false), ("DayEight", Today.AddDays(8), false),
            ("Completed", Today.AddDays(-1), true)
        })
            db.Set<ProjectTask>().Add(new ProjectTask
            {
                ProjectId = id, Title = name, Description = "", DueOn = due,
                IsCompleted = completed, CreatedByUserId = Owner, CreatedAtUtc = DateTime.UtcNow,
                CompletedAtUtc = completed ? DateTime.UtcNow : null,
                CompletedByUserId = completed ? Owner : null
            });
        await db.SaveChangesAsync();
        var alerts = (await new ProjectDeadlineQueryService(db).GetAlertsAsync(Today, default))
            .Where(x => x.ProjectId == id && x.Kind == "Task").ToList();
        Assert.Equal(new[] { "Overdue", "Today", "DaySeven" }, alerts.Select(x => x.Title));
        Assert.True(alerts[0].IsOverdue);
        Assert.All(alerts.Skip(1), x => Assert.False(x.IsOverdue));
    }

    [Fact]
    public async Task Events_ExcludeFulfilledAndVoided_WithoutDroppingPending()
    {
        var (pendingProject, pendingEvent) = await SeedAsync(agreementStatus: "Pending");
        var (fulfilledProject, fulfilledEvent) = await SeedAsync(agreementStatus: "Fulfilled");
        var (voidedProject, voidedEvent) = await SeedAsync(voided: true);
        await using var db = fixture.CreateDbContext();
        foreach (var eventId in new[] { pendingEvent, fulfilledEvent, voidedEvent })
            db.Set<ProjectEventDeadline>().Add(new ProjectEventDeadline
            {
                ProjectEventId = eventId, DueOn = Today.AddDays(-1),
                ChangedByUserId = Owner, ChangedAtUtc = DateTime.UtcNow
            });
        await db.SaveChangesAsync();
        var results = await new ProjectDeadlineQueryService(db).GetAlertsAsync(Today, default);
        Assert.Contains(results, x => x.ProjectId == pendingProject && x.Kind == "Event" && x.IsOverdue);
        Assert.DoesNotContain(results, x => x.ProjectId == fulfilledProject && x.SourceId == fulfilledEvent);
        Assert.DoesNotContain(results, x => x.ProjectId == voidedProject && x.SourceId == voidedEvent);
    }

    [Theory]
    [InlineData(ProjectStatus.Completed)]
    [InlineData(ProjectStatus.Cancelled)]
    [InlineData(ProjectStatus.Archived)]
    public async Task ClosedProjects_AreNotIncluded(ProjectStatus status)
    {
        var (id, eventId) = await SeedAsync(status);
        await using var db = fixture.CreateDbContext();
        db.Set<ProjectTask>().Add(new ProjectTask
        {
            ProjectId = id, Title = "Closed task", Description = "", DueOn = Today,
            CreatedByUserId = Owner, CreatedAtUtc = DateTime.UtcNow
        });
        db.Set<ProjectEventDeadline>().Add(new ProjectEventDeadline
        {
            ProjectEventId = eventId, DueOn = Today, ChangedByUserId = Owner, ChangedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var alerts = await new ProjectDeadlineQueryService(db).GetAlertsAsync(Today, default);
        Assert.DoesNotContain(alerts, x => x.ProjectId == id);
    }
}
