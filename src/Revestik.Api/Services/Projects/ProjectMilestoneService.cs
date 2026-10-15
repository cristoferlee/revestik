using System.Data;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;

namespace Revestik.Api.Services.Projects;

public sealed record MilestoneInput(int? Id, string Name, string? Description, decimal Weight, int SortOrder);
public sealed record MilestoneView(int Id, string Name, string Description, decimal Weight, int SortOrder,
    bool IsCompleted, bool IsExcluded, long Version);
public sealed record ProjectProgressSnapshot(int ProjectId, bool IsConfigured, bool IsActive,
    decimal? Percent, long? PlanVersion, IReadOnlyList<MilestoneView> Milestones);

// Progress is derived here only. Endpoints and UI must never maintain their own calculated percent.
public sealed class ProjectMilestoneService(RevestikDbContext db)
{
    public async Task<ProjectProgressSnapshot?> GetAsync(int projectId, CancellationToken ct)
    {
        if (!await db.Set<Project>().AnyAsync(x => x.Id == projectId, ct)) return null;
        var plan = await db.Set<ProjectMilestonePlan>().AsNoTracking()
            .Include(x => x.Milestones).SingleOrDefaultAsync(x => x.ProjectId == projectId, ct);
        return Snapshot(projectId, plan);
    }

    public async Task<ProjectProgressSnapshot> ConfigureAsync(int projectId,
        IReadOnlyList<MilestoneInput> inputs, long? expectedPlanVersion,
        string actor, bool isAdmin, CancellationToken ct)
    {
        if (inputs is null || inputs.Count > 100) throw Invalid("El plan admite hasta 100 hitos.");
        foreach (var item in inputs) Validate(item);
        var normalized = inputs.Select(x => x.Name.Trim().ToUpperInvariant()).ToArray();
        if (normalized.Distinct(StringComparer.Ordinal).Count() != normalized.Length ||
            inputs.Select(x => x.SortOrder).Distinct().Count() != inputs.Count ||
            inputs.Where(x => x.Id.HasValue).Select(x => x.Id).Distinct().Count() != inputs.Count(x => x.Id.HasValue))
            throw Invalid("Los hitos deben tener nombres, identificadores y posiciones únicos.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await RequireEditorAsync(projectId, actor, isAdmin, ct);
        var plan = await LoadAsync(projectId, ct);
        CheckVersion(plan, expectedPlanVersion);
        if (plan is null)
        {
            if (inputs.Any(x => x.Id.HasValue)) throw Invalid("El hito no existe en este proyecto.");
            plan = new ProjectMilestonePlan { ProjectId = projectId };
            db.Set<ProjectMilestonePlan>().Add(plan);
        }
        else if (inputs.Any(x => x.Id.HasValue && plan.Milestones.All(m => m.Id != x.Id)))
            throw Invalid("El hito no pertenece a este proyecto.");

        var now = DateTime.UtcNow;
        var includedIds = inputs.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();
        var changed = false;
        foreach (var milestone in plan.Milestones.Where(x => !x.IsExcluded && !includedIds.Contains(x.Id)))
        {
            AddRevision(milestone, actor, now, "Excluded", null);
            milestone.IsExcluded = true;
            milestone.Version++;
            changed = true;
        }
        foreach (var input in inputs)
        {
            var name = input.Name.Trim();
            var description = input.Description?.Trim() ?? string.Empty;
            if (input.Id is null)
            {
                plan.Milestones.Add(new ProjectMilestone
                {
                    Name = name, NormalizedName = name.ToUpperInvariant(),
                    Description = description, Weight = input.Weight, SortOrder = input.SortOrder
                });
                changed = true;
                continue;
            }
            var milestone = plan.Milestones.Single(x => x.Id == input.Id.Value);
            if (milestone.Name == name && milestone.Description == description &&
                milestone.Weight == input.Weight && milestone.SortOrder == input.SortOrder && !milestone.IsExcluded)
                continue;
            AddRevision(milestone, actor, now, "Configured", null);
            milestone.Name = name;
            milestone.NormalizedName = name.ToUpperInvariant();
            milestone.Description = description;
            milestone.Weight = input.Weight;
            milestone.SortOrder = input.SortOrder;
            milestone.IsExcluded = false;
            milestone.Version++;
            changed = true;
        }
        if (plan.IsActive) EnsureCompleteWeights(plan);
        if (changed && db.Entry(plan).State != EntityState.Added) plan.Version++;
        await PersistAsync(ct);
        await transaction.CommitAsync(ct);
        return Snapshot(projectId, plan);
    }

    public async Task<ProjectProgressSnapshot> ActivateAsync(int projectId, long expectedPlanVersion,
        string actor, bool isAdmin, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await RequireEditorAsync(projectId, actor, isAdmin, ct);
        var plan = await LoadAsync(projectId, ct);
        CheckVersion(plan, expectedPlanVersion);
        if (plan is null) throw Invalid("Configure los hitos antes de activar el progreso.");
        EnsureCompleteWeights(plan);
        if (!plan.IsActive)
        {
            plan.IsActive = true;
            plan.Version++;
            await PersistAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return Snapshot(projectId, plan);
    }

    public async Task<ProjectProgressSnapshot> SetCompletedAsync(int projectId, int milestoneId,
        bool completed, long expectedPlanVersion, long expectedMilestoneVersion,
        string actor, bool isAdmin, string? reason, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await RequireEditorAsync(projectId, actor, isAdmin, ct);
        var plan = await LoadAsync(projectId, ct);
        CheckVersion(plan, expectedPlanVersion);
        if (plan is null || !plan.IsActive) throw Invalid("El progreso no está activado.");
        var milestone = plan.Milestones.SingleOrDefault(x => x.Id == milestoneId && !x.IsExcluded);
        if (milestone is null) throw Invalid("El hito no existe o está excluido.");
        if (milestone.Version != expectedMilestoneVersion) throw Conflict();
        if (milestone.IsCompleted != completed)
        {
            if (!completed && (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500))
                throw Invalid("La reversión necesita un motivo de hasta 500 caracteres.");
            var now = DateTime.UtcNow;
            AddRevision(milestone, actor, now, completed ? "Completed" : "Reverted", reason?.Trim());
            milestone.IsCompleted = completed;
            milestone.CompletedAtUtc = completed ? now : null;
            milestone.CompletedByUserId = completed ? actor : null;
            milestone.Version++;
            plan.Version++;
            await PersistAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return Snapshot(projectId, plan);
    }

    private Task<ProjectMilestonePlan?> LoadAsync(int projectId, CancellationToken ct) =>
        db.Set<ProjectMilestonePlan>().Include(x => x.Milestones)
            .SingleOrDefaultAsync(x => x.ProjectId == projectId, ct);

    private async Task RequireEditorAsync(int projectId, string actor, bool isAdmin, CancellationToken ct)
    {
        var project = await db.Set<Project>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == projectId, ct);
        if (project is null) throw Invalid("El proyecto no existe.");
        if (!isAdmin && project.ResponsibleUserId != actor)
            throw new ProjectOperationException(ProjectFailureReason.Forbidden,
                "Solo el responsable o un administrador puede modificar el progreso.");
    }

    private static void Validate(MilestoneInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 150 ||
            input.Description?.Length > 2000 || input.Weight <= 0 || input.Weight > 100 ||
            decimal.Round(input.Weight, 2) != input.Weight || input.SortOrder < 0 || input.Id is <= 0)
            throw Invalid("El nombre, descripción, peso u orden del hito no es válido.");
    }

    private static void EnsureCompleteWeights(ProjectMilestonePlan plan)
    {
        var active = plan.Milestones.Where(x => !x.IsExcluded).ToList();
        if (active.Count == 0 || active.Sum(x => x.Weight) != 100m)
            throw Invalid("Los pesos de los hitos activos deben sumar exactamente 100%.");
    }

    private static void CheckVersion(ProjectMilestonePlan? plan, long? expected)
    {
        if (plan is null && expected is null) return;
        if (plan is null || expected != plan.Version) throw Conflict();
    }

    private void AddRevision(ProjectMilestone item, string actor, DateTime time, string kind, string? reason)
    {
        db.Set<ProjectMilestoneRevision>().Add(new ProjectMilestoneRevision
        {
            Milestone = item, PreviousVersion = item.Version, Name = item.Name,
            Description = item.Description, Weight = item.Weight, SortOrder = item.SortOrder,
            WasExcluded = item.IsExcluded, WasCompleted = item.IsCompleted,
            PreviousCompletedAtUtc = item.CompletedAtUtc,
            PreviousCompletedByUserId = item.CompletedByUserId,
            ChangedByUserId = actor, ChangedAtUtc = time, ChangeKind = kind, Reason = reason
        });
    }

    private static ProjectProgressSnapshot Snapshot(int projectId, ProjectMilestonePlan? plan)
    {
        if (plan is null) return new(projectId, false, false, null, null, []);
        var active = plan.Milestones.Where(x => !x.IsExcluded).ToList();
        decimal? percent = plan.IsActive ? active.Where(x => x.IsCompleted).Sum(x => x.Weight) : null;
        return new(projectId, active.Count != 0, plan.IsActive, percent, plan.Version,
            plan.Milestones.OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
                .Select(x => new MilestoneView(x.Id, x.Name, x.Description, x.Weight, x.SortOrder,
                    x.IsCompleted, x.IsExcluded, x.Version)).ToList());
    }

    private async Task PersistAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw Conflict(); }
        catch (DbUpdateException) { throw Conflict(); }
    }

    private static ProjectOperationException Invalid(string message) =>
        new(ProjectFailureReason.Invalid, message);
    private static ProjectOperationException Conflict() =>
        new(ProjectFailureReason.ConcurrencyConflict,
            "El plan de progreso cambió. Actualice los datos antes de continuar.");
}
