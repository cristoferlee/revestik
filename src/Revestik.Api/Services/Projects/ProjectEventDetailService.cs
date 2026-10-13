using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Projects;

namespace Revestik.Api.Services.Projects;

public sealed class ProjectEventDetailService(RevestikDbContext db)
{
    public async Task<IReadOnlyList<ProjectCommentResponse>?> CommentsAsync(int projectId, int eventId, CancellationToken ct)
    {
        if (!await EventExistsAsync(projectId, eventId, ct)) return null;
        return await db.Set<ProjectEventComment>().AsNoTracking()
            .Where(x => x.ProjectEventId == eventId).OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Select(x => new ProjectCommentResponse(x.Id, x.ProjectEventId, x.Text, x.CreatedByUserId, x.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<ProjectCommentResponse?> AddCommentAsync(int projectId, int eventId,
        ProjectCommentCreateRequest request, string actor, bool isAdmin, CancellationToken ct)
    {
        await RequireEditorAsync(projectId, actor, isAdmin, ct);
        var ev = await db.Set<ProjectEvent>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == eventId, ct);
        if (ev is null) return null;
        if (ev.IsVoided) throw Invalid("No se pueden agregar comentarios a eventos anulados.");
        if (request is null || string.IsNullOrWhiteSpace(request.Text) || request.Text.Trim().Length > 4000)
            throw Invalid("El comentario debe tener entre 1 y 4000 caracteres.");
        var comment = new ProjectEventComment { ProjectEventId = eventId, Text = request.Text.Trim(),
            CreatedByUserId = actor, CreatedAtUtc = DateTime.UtcNow };
        db.Set<ProjectEventComment>().Add(comment);
        await db.SaveChangesAsync(ct);
        return new(comment.Id, eventId, comment.Text, actor, comment.CreatedAtUtc);
    }

    public async Task<ProjectAgreementResponse?> GetAgreementAsync(int projectId, int eventId, CancellationToken ct)
    {
        if (!await EventExistsAsync(projectId, eventId, ct)) return null;
        var agreement = await db.Set<ProjectEventAgreement>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProjectEventId == eventId, ct);
        return agreement is null ? null : Map(agreement);
    }

    public async Task<ProjectAgreementResponse?> SaveAgreementAsync(int projectId, int eventId,
        ProjectAgreementUpsertRequest request, string actor, bool isAdmin, CancellationToken ct)
    {
        await RequireEditorAsync(projectId, actor, isAdmin, ct);
        var ev = await db.Set<ProjectEvent>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == eventId, ct);
        if (ev is null) return null;
        if (ev.IsVoided) throw Invalid("No se puede modificar un acuerdo de un evento anulado.");
        Validate(request);
        var agreement = await db.Set<ProjectEventAgreement>()
            .SingleOrDefaultAsync(x => x.ProjectEventId == eventId, ct);
        if (agreement is null)
        {
            if (request.Version is not null and not 0)
                throw Conflict();
            agreement = new ProjectEventAgreement { ProjectEventId = eventId, Version = 1 };
            db.Set<ProjectEventAgreement>().Add(agreement);
        }
        else
        {
            if (request.Version != agreement.Version) throw Conflict();
            db.Set<ProjectEventAgreementRevision>().Add(new ProjectEventAgreementRevision
            {
                ProjectEventId = eventId, PreviousVersion = agreement.Version,
                Status = agreement.Status, AcceptedBy = agreement.AcceptedBy, AcceptedOn = agreement.AcceptedOn,
                AcceptanceMedium = agreement.AcceptanceMedium, Notes = agreement.Notes,
                ChangedByUserId = actor, ChangedAtUtc = DateTime.UtcNow
            });
            agreement.Version++;
        }
        agreement.Status = request.Status.ToString();
        agreement.AcceptedBy = Clean(request.AcceptedBy);
        agreement.AcceptedOn = request.AcceptedOn;
        agreement.AcceptanceMedium = Clean(request.AcceptanceMedium);
        agreement.Notes = Clean(request.Notes);
        agreement.UpdatedAtUtc = DateTime.UtcNow;
        agreement.UpdatedByUserId = actor;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 2601 or 2627)
        { throw Conflict(); }
        return Map(agreement);
    }

    public async Task<IReadOnlyList<ProjectEventRevisionResponse>?> EventHistoryAsync(int projectId, int eventId, CancellationToken ct)
    {
        if (!await EventExistsAsync(projectId, eventId, ct)) return null;
        return await db.Set<ProjectEventRevision>().AsNoTracking()
            .Where(x => x.ProjectEventId == eventId).OrderBy(x => x.PreviousVersion)
            .Select(x => new ProjectEventRevisionResponse(x.Id, x.PreviousVersion, x.CategoryId, x.Title,
                x.Description, x.OccurredOn, x.WasVoided, x.PreviousVoidReason, x.ChangedByUserId,
                x.ChangedAtUtc, x.ChangeKind)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProjectAgreementRevisionResponse>?> AgreementHistoryAsync(int projectId, int eventId, CancellationToken ct)
    {
        if (!await EventExistsAsync(projectId, eventId, ct)) return null;
        var rows = await db.Set<ProjectEventAgreementRevision>().AsNoTracking()
            .Where(x => x.ProjectEventId == eventId).OrderBy(x => x.PreviousVersion).ToListAsync(ct);
        return rows.Select(x => new ProjectAgreementRevisionResponse(x.Id, x.PreviousVersion,
            Enum.Parse<ProjectAgreementStatus>(x.Status), x.AcceptedBy, x.AcceptedOn,
            x.AcceptanceMedium, x.Notes, x.ChangedByUserId, x.ChangedAtUtc)).ToList();
    }

    private Task<bool> EventExistsAsync(int projectId, int eventId, CancellationToken ct) =>
        db.Set<ProjectEvent>().AnyAsync(x => x.Id == eventId && x.ProjectId == projectId, ct);

    private async Task RequireEditorAsync(int projectId, string actor, bool admin, CancellationToken ct)
    {
        var project = await db.Set<Project>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == projectId, ct);
        if (project is null) throw Invalid("El proyecto no existe.");
        if (!admin && project.ResponsibleUserId != actor)
            throw new ProjectOperationException(ProjectFailureReason.Forbidden,
                "Solo el responsable o un administrador puede modificar este proyecto.");
    }

    private static void Validate(ProjectAgreementUpsertRequest request)
    {
        if (request is null || !Enum.IsDefined(request.Status) ||
            request.AcceptedBy?.Length > 150 || request.AcceptanceMedium?.Length > 80 || request.Notes?.Length > 2000)
            throw Invalid("Los datos del acuerdo no son válidos.");
        if ((request.AcceptedBy is null) != (request.AcceptedOn is null))
            throw Invalid("La persona y la fecha de aceptación deben indicarse juntas.");
        if (request.AcceptanceMedium is not null && request.AcceptedBy is null)
            throw Invalid("El medio de aceptación requiere una persona y fecha.");
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static ProjectAgreementResponse Map(ProjectEventAgreement x) => new(x.ProjectEventId,
        Enum.Parse<ProjectAgreementStatus>(x.Status), x.AcceptedBy, x.AcceptedOn, x.AcceptanceMedium,
        x.Notes, x.Version, x.UpdatedAtUtc, x.UpdatedByUserId);
    private static ProjectOperationException Invalid(string message) => new(ProjectFailureReason.Invalid, message);
    private static ProjectOperationException Conflict() => new(ProjectFailureReason.ConcurrencyConflict,
        "El acuerdo cambió. Actualice la información antes de continuar.");
}
