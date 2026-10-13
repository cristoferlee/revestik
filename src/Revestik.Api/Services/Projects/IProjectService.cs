using Revestik.Shared.Projects;

namespace Revestik.Api.Services.Projects;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectSummaryResponse>> ListAsync(int customerId, CancellationToken ct);
    Task<ProjectResponse?> GetAsync(int id, CancellationToken ct);
    Task<ProjectResponse> CreateAsync(int customerId, ProjectUpsertRequest request, string creatorUserId, CancellationToken ct);
    Task<ProjectResponse?> UpdateAsync(int id, ProjectUpsertRequest request, string actorUserId, bool isAdmin, CancellationToken ct);
    Task<ProjectResponse?> ReassignAsync(int id, string newResponsibleUserId, long version, CancellationToken ct);
    Task<bool> DeleteAsync(int id, string actorUserId, bool isAdmin, long version, CancellationToken ct);
}

public enum ProjectFailureReason { Invalid, Duplicate, InactiveCustomer, Forbidden, UserNotFound, ConcurrencyConflict }

public sealed class ProjectOperationException(ProjectFailureReason reason, string message) : Exception(message)
{
    public ProjectFailureReason Reason { get; } = reason;
}
