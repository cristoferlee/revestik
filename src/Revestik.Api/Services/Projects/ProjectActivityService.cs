using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Projects;

namespace Revestik.Api.Services.Projects;

public sealed class ProjectActivityService(RevestikDbContext db)
{
    public async Task<IReadOnlyList<ProjectEventCategoryResponse>> CategoriesAsync(CancellationToken ct) =>
        await db.Set<ProjectEventCategory>().AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new ProjectEventCategoryResponse(x.Id, x.Name, x.IsActive)).ToListAsync(ct);

    public async Task<ProjectEventCategoryResponse> CreateCategoryAsync(string? name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw Invalid("La categoría debe tener entre 1 y 100 caracteres.");
        var category = new ProjectEventCategory { Name = name.Trim(), NormalizedName = name.Trim().ToUpperInvariant() };
        db.Set<ProjectEventCategory>().Add(category);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql &&
            sql.Number is 2601 or 2627 && sql.Message.Contains("IX_ProjectEventCategories_NormalizedName", StringComparison.Ordinal))
        { throw new ProjectOperationException(ProjectFailureReason.Duplicate, "La categoría ya existe."); }
        return new(category.Id, category.Name, category.IsActive);
    }

    public async Task<bool> DeactivateCategoryAsync(int id, CancellationToken ct)
    {
        var category = await db.Set<ProjectEventCategory>().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (category is null) return false;
        category.IsActive = false;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<ProjectEventResponse>?> ListAsync(int projectId, ProjectEventSearchRequest filter, CancellationToken ct)
    {
        if (!await db.Set<Project>().AnyAsync(x => x.Id == projectId, ct)) return null;
        if (filter.DateFrom > filter.DateTo) throw Invalid("El rango de fechas es inválido.");
        var query = db.Set<ProjectEvent>().AsNoTracking().Where(x => x.ProjectId == projectId);
        if (!filter.IncludeVoided) query = query.Where(x => !x.IsVoided);
        if (filter.CategoryId is int category) query = query.Where(x => x.CategoryId == category);
        if (filter.DateFrom is DateOnly start) query = query.Where(x => x.OccurredOn >= start);
        if (filter.DateTo is DateOnly end) query = query.Where(x => x.OccurredOn <= end);
        if (!string.IsNullOrWhiteSpace(filter.CreatedByUserId)) query = query.Where(x => x.CreatedByUserId == filter.CreatedByUserId);
        return await query.OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.Id)
            .Select(x => new ProjectEventResponse(x.Id, x.ProjectId, x.CategoryId, x.Category.Name,
                x.Title, x.Description, x.OccurredOn, x.CreatedByUserId, x.CreatedAtUtc,
                x.UpdatedByUserId, x.UpdatedAtUtc, x.IsVoided, x.VoidReason, x.Version)).ToListAsync(ct);
    }

    public async Task<ProjectEventResponse?> GetAsync(int projectId, int eventId, CancellationToken ct) =>
        await db.Set<ProjectEvent>().AsNoTracking().Where(x => x.ProjectId == projectId && x.Id == eventId)
            .Select(x => new ProjectEventResponse(x.Id, x.ProjectId, x.CategoryId, x.Category.Name,
                x.Title, x.Description, x.OccurredOn, x.CreatedByUserId, x.CreatedAtUtc,
                x.UpdatedByUserId, x.UpdatedAtUtc, x.IsVoided, x.VoidReason, x.Version)).SingleOrDefaultAsync(ct);

    public async Task<ProjectEventResponse> CreateAsync(int projectId, ProjectEventUpsertRequest request,
        string actorId, bool isAdmin, CancellationToken ct)
    {
        await RequireEditorAsync(projectId, actorId, isAdmin, ct);
        Validate(request);
        await RequireActiveCategoryAsync(request.CategoryId, ct);
        var ev = new ProjectEvent { ProjectId = projectId, CategoryId = request.CategoryId,
            Title = request.Title.Trim(), Description = request.Description?.Trim() ?? string.Empty,
            OccurredOn = request.OccurredOn, CreatedByUserId = actorId, CreatedAtUtc = DateTime.UtcNow };
        db.Set<ProjectEvent>().Add(ev);
        await SaveAsync(ct);
        return (await GetAsync(projectId, ev.Id, ct))!;
    }

    public async Task<ProjectEventResponse?> UpdateAsync(int projectId, int eventId, ProjectEventUpsertRequest request,
        string actorId, bool isAdmin, CancellationToken ct)
    {
        await RequireEditorAsync(projectId, actorId, isAdmin, ct);
        var ev = await db.Set<ProjectEvent>().SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == eventId, ct);
        if (ev is null) return null;
        if (ev.IsVoided) throw Invalid("Un evento anulado no puede modificarse.");
        RequireVersion(ev, request.Version);
        Validate(request);
        await RequireActiveCategoryAsync(request.CategoryId, ct);
        Snapshot(ev, actorId, "Updated");
        ev.CategoryId = request.CategoryId;
        ev.Title = request.Title.Trim();
        ev.Description = request.Description?.Trim() ?? string.Empty;
        ev.OccurredOn = request.OccurredOn;
        ev.Version++;
        ev.UpdatedAtUtc = DateTime.UtcNow;
        ev.UpdatedByUserId = actorId;
        await SaveAsync(ct);
        return (await GetAsync(projectId, ev.Id, ct))!;
    }

    public async Task<ProjectEventResponse?> VoidAsync(int projectId, int eventId, ProjectEventVoidRequest request,
        string actorId, bool isAdmin, CancellationToken ct)
    {
        await RequireEditorAsync(projectId, actorId, isAdmin, ct);
        var ev = await db.Set<ProjectEvent>().SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == eventId, ct);
        if (ev is null) return null;
        if (ev.IsVoided) throw Invalid("El evento ya está anulado.");
        RequireVersion(ev, request.Version);
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
            throw Invalid("La anulación requiere un motivo de hasta 500 caracteres.");
        Snapshot(ev, actorId, "Voided");
        ev.IsVoided = true;
        ev.VoidReason = request.Reason.Trim();
        ev.Version++;
        ev.UpdatedAtUtc = DateTime.UtcNow;
        ev.UpdatedByUserId = actorId;
        await SaveAsync(ct);
        return (await GetAsync(projectId, ev.Id, ct))!;
    }

    private void Snapshot(ProjectEvent ev, string actor, string kind) =>
        db.Set<ProjectEventRevision>().Add(new ProjectEventRevision
        {
            ProjectEventId = ev.Id, PreviousVersion = ev.Version, CategoryId = ev.CategoryId,
            Title = ev.Title, Description = ev.Description, OccurredOn = ev.OccurredOn,
            WasVoided = ev.IsVoided, PreviousVoidReason = ev.VoidReason,
            ChangedByUserId = actor, ChangedAtUtc = DateTime.UtcNow, ChangeKind = kind
        });

    private async Task RequireEditorAsync(int projectId, string actor, bool isAdmin, CancellationToken ct)
    {
        var project = await db.Set<Project>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == projectId, ct);
        if (project is null) throw new ProjectOperationException(ProjectFailureReason.Invalid, "El proyecto no existe.");
        if (!isAdmin && project.ResponsibleUserId != actor)
            throw new ProjectOperationException(ProjectFailureReason.Forbidden, "Solo el responsable o un administrador puede editar eventos.");
        if (project.Status is ProjectStatus.Completed or ProjectStatus.Cancelled or ProjectStatus.Archived)
            throw new ProjectOperationException(ProjectFailureReason.Invalid,
                "El proyecto debe reabrirse o reactivarse antes de modificar su actividad.");
    }

    private async Task RequireActiveCategoryAsync(int categoryId, CancellationToken ct)
    {
        if (!await db.Set<ProjectEventCategory>().AnyAsync(x => x.Id == categoryId && x.IsActive, ct))
            throw Invalid("Seleccione una categoría activa.");
    }

    private static void Validate(ProjectEventUpsertRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200 ||
            request.Description?.Length > 4000 || request.OccurredOn == default)
            throw Invalid("El título, descripción o fecha del evento no son válidos.");
    }

    private static void RequireVersion(ProjectEvent ev, long? version)
    {
        if (version is null or <= 0) throw Invalid("Se requiere la versión del evento.");
        if (ev.Version != version) throw new ProjectOperationException(ProjectFailureReason.ConcurrencyConflict,
            "El evento ha cambiado. Vuelva a consultar antes de modificarlo.");
    }

    private static ProjectOperationException Invalid(string detail) => new(ProjectFailureReason.Invalid, detail);

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { throw new ProjectOperationException(ProjectFailureReason.ConcurrencyConflict,
            "El evento se modificó simultáneamente. Vuelva a consultar."); }
    }
}
