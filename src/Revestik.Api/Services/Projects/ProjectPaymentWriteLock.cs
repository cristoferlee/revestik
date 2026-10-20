using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Projects;

namespace Revestik.Api.Services.Projects;

// Every operation that can change a project's available balance acquires the same row lock.
internal static class ProjectPaymentWriteLock
{
    internal static async Task<Project> LoadAsync(RevestikDbContext db, int projectId,
        string actor, bool admin, CancellationToken ct)
    {
        var project = await db.Set<Project>()
            .FromSqlInterpolated($"SELECT * FROM dbo.Projects WITH (UPDLOCK, HOLDLOCK) WHERE Id = {projectId}")
            .SingleOrDefaultAsync(ct);
        if (project is null) throw new ProjectOperationException(ProjectFailureReason.Invalid, "El proyecto no existe.");
        if (string.IsNullOrWhiteSpace(actor) || (!admin && project.ResponsibleUserId != actor))
            throw new ProjectOperationException(ProjectFailureReason.Forbidden,
                "Solo el responsable o un administrador puede modificar pagos del proyecto.");
        if (project.Status is ProjectStatus.Completed or ProjectStatus.Cancelled or ProjectStatus.Archived)
            throw new ProjectOperationException(ProjectFailureReason.Invalid,
                "Un proyecto finalizado, cancelado o archivado no admite cambios financieros ni de comprobantes.");
        return project;
    }
}
