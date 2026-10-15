using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Projects;

namespace Revestik.Api.Services.Projects;

public sealed class ProjectMilestoneTemplateService(RevestikDbContext db, ProjectMilestoneService milestones)
{
    public async Task<IReadOnlyList<ProjectMilestoneTemplateResponse>> ListAsync(CancellationToken ct)
    {
        var templates = await db.Set<ProjectMilestoneTemplate>().AsNoTracking()
            .Include(x => x.Items).OrderBy(x => x.Name).ToListAsync(ct);
        return templates.Select(Map).ToList();
    }

    public async Task<ProjectMilestoneTemplateResponse> CreateAsync(
        CreateProjectMilestoneTemplateRequest request, string actor, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name) ||
            request.Name.Trim().Length > 150 || request.Description?.Length > 2000 ||
            request.Items is null || request.Items.Count is < 1 or > 100)
            throw Invalid("Los datos de la plantilla no son válidos.");

        var names = new HashSet<string>(StringComparer.Ordinal);
        var orders = new HashSet<int>();
        decimal total = 0;
        foreach (var item in request.Items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Name) || item.Name.Trim().Length > 150 ||
                item.Description?.Length > 2000 || item.Weight <= 0 || item.Weight > 100 ||
                decimal.Round(item.Weight, 2) != item.Weight || item.SortOrder < 0 ||
                !names.Add(item.Name.Trim().ToUpperInvariant()) || !orders.Add(item.SortOrder))
                throw Invalid("Los hitos de la plantilla contienen datos o duplicados inválidos.");
            total += item.Weight;
        }
        if (total != 100m) throw Invalid("Los pesos de la plantilla deben sumar exactamente 100%.");

        var name = request.Name.Trim();
        var template = new ProjectMilestoneTemplate
        {
            Name = name, NormalizedName = name.ToUpperInvariant(),
            Description = request.Description?.Trim() ?? string.Empty,
            CreatedByUserId = actor,
            Items = request.Items.Select(x => new ProjectMilestoneTemplateItem
            {
                Name = x.Name.Trim(), Description = x.Description?.Trim() ?? string.Empty,
                Weight = x.Weight, SortOrder = x.SortOrder
            }).ToList()
        };
        db.Set<ProjectMilestoneTemplate>().Add(template);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number is 2601 or 2627)
        {
            throw new ProjectOperationException(ProjectFailureReason.Duplicate, "Ya existe una plantilla con ese nombre.");
        }
        return Map(template);
    }

    public async Task<bool> DeactivateAsync(int templateId, CancellationToken ct)
    {
        var template = await db.Set<ProjectMilestoneTemplate>().SingleOrDefaultAsync(x => x.Id == templateId, ct);
        if (template is null) return false;
        if (template.IsActive)
        {
            template.IsActive = false;
            await db.SaveChangesAsync(ct);
        }
        return true;
    }

    public async Task<ProjectProgressSnapshot> ApplyAsync(int projectId, int templateId,
        long? planVersion, string actor, bool isAdmin, CancellationToken ct)
    {
        var template = await db.Set<ProjectMilestoneTemplate>().AsNoTracking()
            .Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == templateId && x.IsActive, ct);
        if (template is null) throw Invalid("La plantilla no existe o está inactiva.");
        var existing = await milestones.GetAsync(projectId, ct);
        if (existing is null) throw Invalid("El proyecto no existe.");
        // Avoid silently replacing existing progress, especially already completed milestones.
        if (existing.IsConfigured || existing.Milestones.Count != 0)
            throw Invalid("La plantilla solo puede aplicarse a un proyecto sin hitos configurados.");
        var inputs = template.Items.Select(x => new MilestoneInput(null, x.Name, x.Description,
            x.Weight, x.SortOrder)).ToList();
        return await milestones.ConfigureAsync(projectId, inputs, planVersion, actor, isAdmin, ct);
    }

    private static ProjectMilestoneTemplateResponse Map(ProjectMilestoneTemplate x) => new(
        x.Id, x.Name, x.Description, x.IsActive,
        x.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .Select(i => new ProjectMilestoneTemplateItemResponse(
                i.Id, i.Name, i.Description, i.Weight, i.SortOrder)).ToList());

    private static ProjectOperationException Invalid(string message) =>
        new(ProjectFailureReason.Invalid, message);
}
