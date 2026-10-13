using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Projects;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Customers;
using Revestik.Shared.Projects;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectEventDetailSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private async Task<(int ProjectId, int EventId)> SeedAsync()
    {
        await using var db = fixture.CreateDbContext();
        if (!await db.Users.AnyAsync(x => x.Id == "detail-owner"))
            db.Users.Add(new ApplicationUser
            {
                Id = "detail-owner", UserName = "detail-owner", NormalizedUserName = "DETAIL-OWNER",
                Email = "detail-owner@example.com", NormalizedEmail = "DETAIL-OWNER@EXAMPLE.COM",
                DisplayName = "Owner", SecurityStamp = Guid.NewGuid().ToString()
            });
        var customer = new Customer
        {
            Name = "Cliente detalle", IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9], Email = "detail@example.com",
            PhoneNumber = "88888888", ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
            OtherSigns = "Heredia", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var project = new Project
        {
            CustomerId = customer.Id, ResponsibleUserId = "detail-owner",
            Name = $"Proyecto {Guid.NewGuid():N}", NormalizedName = Guid.NewGuid().ToString("N").ToUpperInvariant()
        };
        var category = new ProjectEventCategory
        {
            Name = $"Detalle {Guid.NewGuid():N}", NormalizedName = Guid.NewGuid().ToString("N").ToUpperInvariant()
        };
        db.Set<Project>().Add(project);
        db.Set<ProjectEventCategory>().Add(category);
        await db.SaveChangesAsync();
        var ev = new ProjectEvent
        {
            ProjectId = project.Id, CategoryId = category.Id, Title = "Visita", Description = "Inspección",
            OccurredOn = new DateOnly(2026, 10, 1), CreatedByUserId = "detail-owner", CreatedAtUtc = DateTime.UtcNow
        };
        db.Set<ProjectEvent>().Add(ev);
        await db.SaveChangesAsync();
        return (project.Id, ev.Id);
    }

    [Fact]
    public async Task Comments_AreAppendOnlyAndAttributable()
    {
        var (projectId, eventId) = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var service = new ProjectEventDetailService(db);
        await service.AddCommentAsync(projectId, eventId, new("Primero"), "detail-owner", false, default);
        await service.AddCommentAsync(projectId, eventId, new("Segundo"), "detail-owner", false, default);
        var comments = await service.CommentsAsync(projectId, eventId, default);
        Assert.NotNull(comments);
        Assert.Equal(new[] { "Primero", "Segundo" }, comments.Select(x => x.Text));
        Assert.All(comments, x => Assert.Equal("detail-owner", x.CreatedByUserId));
    }

    [Fact]
    public async Task NonOwner_CannotAppendCommentsOrSaveAgreements()
    {
        var (projectId, eventId) = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var service = new ProjectEventDetailService(db);
        var comment = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            service.AddCommentAsync(projectId, eventId, new("No autorizado"), "outsider", false, default));
        Assert.Equal(ProjectFailureReason.Forbidden, comment.Reason);
        var agreement = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            service.SaveAgreementAsync(projectId, eventId,
                new(ProjectAgreementStatus.Pending, null, null, null, null), "outsider", false, default));
        Assert.Equal(ProjectFailureReason.Forbidden, agreement.Reason);
    }

    [Fact]
    public async Task AgreementUpdate_PreservesPreviousVersionAndRejectsStaleVersion()
    {
        var (projectId, eventId) = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var service = new ProjectEventDetailService(db);
        var first = await service.SaveAgreementAsync(projectId, eventId,
            new(ProjectAgreementStatus.Pending, null, null, null, "Pendiente"), "detail-owner", false, default);
        Assert.NotNull(first);
        var next = await service.SaveAgreementAsync(projectId, eventId,
            new(ProjectAgreementStatus.Fulfilled, "Cliente", new DateOnly(2026, 10, 2), "WhatsApp", "Aceptado", first!.Version),
            "detail-owner", false, default);
        Assert.Equal(first.Version + 1, next!.Version);
        var history = await service.AgreementHistoryAsync(projectId, eventId, default);
        Assert.NotNull(history);
        var previous = Assert.Single(history);
        Assert.Equal(ProjectAgreementStatus.Pending, previous.Status);
        Assert.Equal("Pendiente", previous.Notes);
        var stale = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            service.SaveAgreementAsync(projectId, eventId,
                new(ProjectAgreementStatus.Modified, null, null, null, null, first.Version),
                "detail-owner", false, default));
        Assert.Equal(ProjectFailureReason.ConcurrencyConflict, stale.Reason);
    }

    [Fact]
    public async Task VoidedEvent_RejectsNewCommentAndAgreementModification()
    {
        var (projectId, eventId) = await SeedAsync();
        await using (var db = fixture.CreateDbContext())
        {
            var ev = await db.Set<ProjectEvent>().SingleAsync(x => x.Id == eventId);
            ev.IsVoided = true;
            await db.SaveChangesAsync();
        }
        await using var check = fixture.CreateDbContext();
        var service = new ProjectEventDetailService(check);
        await Assert.ThrowsAsync<ProjectOperationException>(() =>
            service.AddCommentAsync(projectId, eventId, new("Comentario"), "detail-owner", false, default));
        await Assert.ThrowsAsync<ProjectOperationException>(() =>
            service.SaveAgreementAsync(projectId, eventId,
                new(ProjectAgreementStatus.Pending, null, null, null, null), "detail-owner", false, default));
    }

    [Fact]
    public async Task EventScopedOperations_DoNotAccessDifferentProject()
    {
        var (projectId, eventId) = await SeedAsync();
        var (otherProjectId, _) = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var service = new ProjectEventDetailService(db);
        Assert.Null(await service.CommentsAsync(otherProjectId, eventId, default));
        Assert.Null(await service.GetAgreementAsync(otherProjectId, eventId, default));
        Assert.Null(await service.EventHistoryAsync(otherProjectId, eventId, default));
        Assert.Null(await service.AgreementHistoryAsync(otherProjectId, eventId, default));
    }
}
