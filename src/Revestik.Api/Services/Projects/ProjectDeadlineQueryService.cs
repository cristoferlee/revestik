using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Projects;
namespace Revestik.Api.Services.Projects;
public sealed class ProjectDeadlineQueryService(RevestikDbContext db)
{
    // Query-only: never auto-closes a task, agreement, or project.
    public async Task<IReadOnlyList<ProjectDashboardAlertResponse>> GetAlertsAsync(DateOnly today, CancellationToken ct)
    {
        var until = today.AddDays(7);
        var excluded = new[] { ProjectStatus.Completed, ProjectStatus.Cancelled, ProjectStatus.Archived };
        var tasks = await db.Set<ProjectTask>().AsNoTracking()
            .Where(x => !x.IsCompleted && x.DueOn != null && x.DueOn <= until &&
                        !excluded.Contains(x.Project.Status))
            .Select(x => new { x.ProjectId, Name = x.Project.Name, x.Id, x.Title, Due = x.DueOn!.Value })
            .ToListAsync(ct);
        var events = await db.Set<ProjectEventDeadline>().AsNoTracking()
            .Where(x => x.DueOn != null && x.DueOn <= until && !x.ProjectEvent.IsVoided &&
                        !excluded.Contains(x.ProjectEvent.Project.Status) &&
                        (!db.Set<ProjectEventAgreement>().Any(a => a.ProjectEventId == x.ProjectEventId) ||
                         db.Set<ProjectEventAgreement>().Any(a => a.ProjectEventId == x.ProjectEventId &&
                             a.Status != "Fulfilled")))
            .Select(x => new { ProjectId = x.ProjectEvent.ProjectId, Name = x.ProjectEvent.Project.Name,
                Id = x.ProjectEventId, x.ProjectEvent.Title, Due = x.DueOn!.Value })
            .ToListAsync(ct);
        return tasks.Select(x => new ProjectDashboardAlertResponse(x.ProjectId, x.Name, "Task", x.Id,
                x.Title, x.Due, x.Due < today))
            .Concat(events.Select(x => new ProjectDashboardAlertResponse(x.ProjectId, x.Name,
                "Event", x.Id, x.Title, x.Due, x.Due < today)))
            .OrderBy(x => x.DueOn).ThenBy(x => x.ProjectId).ThenBy(x => x.Kind).ThenBy(x => x.SourceId)
            .ToList();
    }
}
