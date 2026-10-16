using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Projects;

namespace Revestik.Api.Services.Projects;

public sealed class ProjectTaskDeadlineService(RevestikDbContext db)
{
    public async Task<IReadOnlyList<ProjectTaskResponse>?> TasksAsync(int projectId, CancellationToken ct)
    {
        if (!await db.Set<Project>().AnyAsync(p => p.Id == projectId, ct)) return null;
        return (await db.Set<ProjectTask>().AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.IsCompleted).ThenBy(x => x.DueOn).ThenBy(x => x.Id).ToListAsync(ct))
            .Select(Map).ToList();
    }

    public async Task<ProjectTaskResponse?> TaskAsync(int projectId, int taskId, CancellationToken ct)
    {
        var task = await db.Set<ProjectTask>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == taskId, ct);
        return task is null ? null : Map(task);
    }

    public async Task<ProjectTaskResponse> CreateTaskAsync(int projectId, ProjectTaskCreateRequest request,
        string actor, bool admin, CancellationToken ct)
    {
        await RequireEditorAsync(projectId, actor, admin, ct);
        ValidateTask(request.Title, request.Description, request.AssignedToUserId);
        await ValidateAssigneeAsync(request.AssignedToUserId, ct);
        var task = new ProjectTask
        {
            ProjectId = projectId, Title = request.Title.Trim(), Description = request.Description?.Trim() ?? "",
            AssignedToUserId = TrimOrNull(request.AssignedToUserId), DueOn = request.DueOn,
            CreatedByUserId = actor, CreatedAtUtc = DateTime.UtcNow
        };
        db.Set<ProjectTask>().Add(task);
        await PersistAsync(ct);
        return Map(task);
    }

    public async Task<ProjectTaskResponse?> UpdateTaskAsync(int projectId, int taskId, ProjectTaskUpdateRequest request,
        string actor, bool admin, CancellationToken ct)
    {
        await RequireEditorAsync(projectId, actor, admin, ct);
        var task = await db.Set<ProjectTask>().SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == taskId, ct);
        if (task is null) return null;
        if (request.Version != task.Version) throw Conflict();
        ValidateTask(request.Title, request.Description, request.AssignedToUserId);
        ValidateReason(request.Reason);
        await ValidateAssigneeAsync(request.AssignedToUserId, ct);
        var now = DateTime.UtcNow;
        db.Set<ProjectTaskRevision>().Add(new ProjectTaskRevision
        {
            ProjectTaskId = task.Id, PreviousVersion = task.Version, Title = task.Title,
            Description = task.Description, AssignedToUserId = task.AssignedToUserId,
            DueOn = task.DueOn, WasCompleted = task.IsCompleted,
            PreviousCompletedAtUtc = task.CompletedAtUtc, PreviousCompletedByUserId = task.CompletedByUserId,
            ChangedByUserId = actor, ChangedAtUtc = now, Reason = request.Reason.Trim()
        });
        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim() ?? "";
        task.AssignedToUserId = TrimOrNull(request.AssignedToUserId);
        task.DueOn = request.DueOn;
        if (task.IsCompleted != request.IsCompleted)
        {
            task.CompletedAtUtc = request.IsCompleted ? now : null;
            task.CompletedByUserId = request.IsCompleted ? actor : null;
        }
        task.IsCompleted = request.IsCompleted;
        task.UpdatedByUserId = actor;
        task.UpdatedAtUtc = now;
        task.Version++;
        await PersistAsync(ct);
        return Map(task);
    }

    public async Task<IReadOnlyList<ProjectTaskRevisionResponse>?> TaskHistoryAsync(
        int projectId, int taskId, CancellationToken ct)
    {
        if (!await db.Set<ProjectTask>().AnyAsync(x => x.ProjectId == projectId && x.Id == taskId, ct)) return null;
        return await db.Set<ProjectTaskRevision>().AsNoTracking().Where(x => x.ProjectTaskId == taskId)
            .OrderBy(x => x.PreviousVersion)
            .Select(x => new ProjectTaskRevisionResponse(x.PreviousVersion, x.Title, x.Description,
                x.AssignedToUserId, x.DueOn, x.WasCompleted, x.ChangedByUserId, x.ChangedAtUtc, x.Reason))
            .ToListAsync(ct);
    }

    public async Task<ProjectEventDeadlineResponse?> GetDeadlineAsync(int projectId, int eventId, CancellationToken ct)
    {
        if (!await db.Set<ProjectEvent>().AnyAsync(x => x.ProjectId == projectId && x.Id == eventId, ct)) return null;
        var deadline = await db.Set<ProjectEventDeadline>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProjectEventId == eventId, ct);
        return deadline is null ? new(eventId, null, null, null, null) : Map(deadline);
    }

    public async Task<ProjectEventDeadlineResponse?> SetDeadlineAsync(int projectId, int eventId,
        ProjectEventDeadlineSetRequest request, string actor, bool admin, CancellationToken ct)
    {
        await RequireEditorAsync(projectId, actor, admin, ct);
        var ev = await db.Set<ProjectEvent>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == eventId, ct);
        if (ev is null) return null;
        if (ev.IsVoided) throw Invalid("No puede establecerse un vencimiento en un evento anulado.");
        var deadline = await db.Set<ProjectEventDeadline>()
            .SingleOrDefaultAsync(x => x.ProjectEventId == eventId, ct);
        if (deadline is null)
        {
            if (request.Version is not null and not 0) throw Conflict();
            if (request.DueOn is null) return new(eventId, null, null, null, null);
            deadline = new ProjectEventDeadline { ProjectEventId = eventId, DueOn = request.DueOn.Value,
                ChangedByUserId = actor, ChangedAtUtc = DateTime.UtcNow };
            db.Set<ProjectEventDeadline>().Add(deadline);
        }
        else
        {
            if (request.Version != deadline.Version) throw Conflict();
            if (deadline.DueOn == request.DueOn) return Map(deadline);
            ValidateReason(request.Reason);
            var now = DateTime.UtcNow;
            db.Set<ProjectEventDeadlineRevision>().Add(new ProjectEventDeadlineRevision
            {
                ProjectEventId = eventId, PreviousVersion = deadline.Version, PreviousDueOn = deadline.DueOn,
                ChangedByUserId = actor, ChangedAtUtc = now, Reason = request.Reason.Trim()
            });
            // A clear leaves the row with a nullable date instead of erasing history.
            deadline.DueOn = request.DueOn;
            deadline.Version++;
            deadline.ChangedByUserId = actor;
            deadline.ChangedAtUtc = now;
        }
        await PersistAsync(ct);
        return Map(deadline);
    }

    public async Task<IReadOnlyList<ProjectEventDeadlineRevisionResponse>?> DeadlineHistoryAsync(
        int projectId, int eventId, CancellationToken ct)
    {
        if (!await db.Set<ProjectEvent>().AnyAsync(x => x.ProjectId == projectId && x.Id == eventId, ct)) return null;
        return await db.Set<ProjectEventDeadlineRevision>().AsNoTracking()
            .Where(x => x.ProjectEventId == eventId).OrderBy(x => x.PreviousVersion)
            .Select(x => new ProjectEventDeadlineRevisionResponse(x.PreviousVersion, x.PreviousDueOn,
                x.ChangedByUserId, x.ChangedAtUtc, x.Reason)).ToListAsync(ct);
    }

    private async Task RequireEditorAsync(int projectId, string actor, bool admin, CancellationToken ct)
    {
        var p = await db.Set<Project>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == projectId, ct);
        if (p is null) throw Invalid("El proyecto no existe.");
        if (!admin && p.ResponsibleUserId != actor)
            throw new ProjectOperationException(ProjectFailureReason.Forbidden,
                "Solo el responsable o un administrador puede modificar el proyecto.");
        if (p.Status is ProjectStatus.Completed or ProjectStatus.Cancelled or ProjectStatus.Archived)
            throw new ProjectOperationException(ProjectFailureReason.Invalid,
                "El proyecto debe reabrirse o reactivarse antes de modificar tareas o vencimientos.");
    }

    private async Task ValidateAssigneeAsync(string? id, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(id) && !await db.Users.AnyAsync(x => x.Id == id.Trim(), ct))
            throw Invalid("El usuario asignado no existe.");
    }
    private static void ValidateTask(string title, string? description, string? assignee)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200 ||
            description?.Length > 4000 || assignee?.Length > 450)
            throw Invalid("Los datos de la tarea no son válidos.");
    }
    private static void ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw Invalid("El motivo debe tener entre 1 y 500 caracteres.");
    }
    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ProjectTaskResponse Map(ProjectTask x) => new(x.Id, x.ProjectId, x.Title, x.Description,
        x.AssignedToUserId, x.DueOn, x.IsCompleted, x.CompletedAtUtc, x.CompletedByUserId,
        x.Version, x.CreatedByUserId, x.CreatedAtUtc);
    private static ProjectEventDeadlineResponse Map(ProjectEventDeadline x) => new(x.ProjectEventId,
        x.DueOn, x.Version, x.ChangedByUserId, x.ChangedAtUtc);
    private async Task PersistAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 2601 or 2627)
        { throw Conflict(); }
    }
    private static ProjectOperationException Invalid(string message) => new(ProjectFailureReason.Invalid, message);
    private static ProjectOperationException Conflict() => new(ProjectFailureReason.ConcurrencyConflict,
        "Los datos cambiaron. Actualice la información antes de continuar.");
}
