using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Projects;
namespace Revestik.Api.Services.Projects;
public sealed class ProjectLifecycleService(RevestikDbContext db)
{
    public async Task<IReadOnlyList<ProjectLifecycleHistoryResponse>?> HistoryAsync(int id, CancellationToken ct)
    {
        if (!await db.Set<Project>().AnyAsync(x => x.Id == id, ct)) return null;
        return await db.Set<ProjectLifecycleChange>().AsNoTracking().Where(x => x.ProjectId == id)
            .OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id)
            .Select(x => new ProjectLifecycleHistoryResponse(x.Id, x.ProjectId, x.PreviousProjectVersion,
                x.PreviousStatus, x.NewStatus, x.Action, x.Reason, x.ClosedOn,
                x.ClosingObservations, x.EvidenceDocumentId, x.ActorUserId, x.ChangedAtUtc)).ToListAsync(ct);
    }
    public Task<ProjectLifecycleStatusResponse?> TransitionAsync(int id, ProjectTransitionRequest req,
        string actor, bool admin, CancellationToken ct) =>
        ChangeAsync(id, req.Status, req.Version, "Transition", req.Reason, null, null, null, actor, admin, ct);

    public Task<ProjectLifecycleStatusResponse?> CloseAsync(int id, ProjectCloseRequest req,
        string actor, bool admin, CancellationToken ct) =>
        ChangeAsync(id, ProjectStatus.Completed, req.Version, "Close", null, req.ClosedOn,
            req.Observations, req.EvidenceDocumentId, actor, admin, ct);

    public Task<ProjectLifecycleStatusResponse?> ReopenAsync(int id, ProjectReopenRequest req,
        string actor, bool admin, CancellationToken ct) =>
        ChangeAsync(id, req.Status, req.Version, "Reopen", req.Reason, null, null, null, actor, admin, ct);

    private async Task<ProjectLifecycleStatusResponse?> ChangeAsync(int id, ProjectStatus target, long expected,
        string kind, string? reason, DateOnly? closedOn, string? observations, int? evidence,
        string actor, bool admin, CancellationToken ct)
    {
        if (!Enum.IsDefined(target) || expected <= 0 || string.IsNullOrWhiteSpace(actor) ||
            reason?.Length > 500 || observations?.Length > 2000)
            throw Invalid("Datos de transición inválidos.");
        if (kind == "Close" && (closedOn is null || closedOn.Value == default))
            throw Invalid("Debe indicar la fecha real de cierre.");
        if (kind == "Reopen" && (target is not (ProjectStatus.Planning or ProjectStatus.Active) ||
            string.IsNullOrWhiteSpace(reason)))
            throw Invalid("La reapertura requiere motivo y destino Planning o Active.");
        if (kind == "Transition" && target == ProjectStatus.Completed)
            throw Invalid("Para completar un proyecto utilice su cierre formal.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        // Existing B05 project-row locking also serializes status changes against payment/deletion operations.
        var project = await db.Set<Project>()
            .FromSqlInterpolated($"SELECT * FROM dbo.Projects WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id}")
            .SingleOrDefaultAsync(ct);
        if (project is null) return null;
        if (!admin && project.ResponsibleUserId != actor)
            throw new ProjectOperationException(ProjectFailureReason.Forbidden,
                "Solo el responsable o un administrador puede cambiar el estado.");
        if (project.Version != expected) throw Conflict();
        if (project.Status == target) throw Invalid("El proyecto ya tiene ese estado.");
        if (kind == "Close" && project.Status == ProjectStatus.Completed)
            throw Invalid("El proyecto ya está completado.");
        if (kind == "Reopen" && project.Status != ProjectStatus.Completed)
            throw Invalid("Solamente un proyecto completado puede reabrirse por esta operación.");
        if (kind == "Close" && project.Status is ProjectStatus.Cancelled or ProjectStatus.Archived)
            throw Invalid("Primero reactive el proyecto mediante una transición de estado.");
        if (kind == "Transition" && project.Status == ProjectStatus.Completed)
            throw Invalid("Utilice la operación de reapertura para un proyecto completado.");
        if (kind == "Transition" && target is ProjectStatus.Cancelled or ProjectStatus.Archived &&
            string.IsNullOrWhiteSpace(reason))
            throw Invalid("La cancelación o archivo requiere un motivo.");
        if (evidence is int documentId && !await db.Set<ProjectDocument>()
                .AnyAsync(x => x.Id == documentId && x.ProjectId == id, ct))
            throw Invalid("La evidencia no pertenece a este proyecto.");
        db.Set<ProjectLifecycleChange>().Add(new ProjectLifecycleChange
        {
            ProjectId = id, PreviousProjectVersion = project.Version,
            PreviousStatus = project.Status, NewStatus = target, Action = kind,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            ClosedOn = closedOn, ClosingObservations = observations?.Trim(), EvidenceDocumentId = evidence,
            ActorUserId = actor, ChangedAtUtc = DateTime.UtcNow
        });
        project.Status = target;
        project.Version++;
        project.UpdatedAtUtc = DateTime.UtcNow;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw Conflict(); }
        await tx.CommitAsync(ct);
        return new(id, project.Status, project.Version);
    }
    private static ProjectOperationException Invalid(string message) => new(ProjectFailureReason.Invalid, message);
    private static ProjectOperationException Conflict() => new(ProjectFailureReason.ConcurrencyConflict,
        "El estado del proyecto cambió. Actualice la información.");
}
