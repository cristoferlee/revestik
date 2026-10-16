namespace Revestik.Shared.Projects;
public sealed record ProjectTransitionRequest(ProjectStatus Status, long Version, string? Reason);
public sealed record ProjectCloseRequest(DateOnly ClosedOn, string? Observations, int? EvidenceDocumentId, long Version);
public sealed record ProjectReopenRequest(ProjectStatus Status, long Version, string Reason);
public sealed record ProjectLifecycleHistoryResponse(long Id, int ProjectId, long PreviousProjectVersion,
    ProjectStatus PreviousStatus, ProjectStatus NewStatus, string Action, string? Reason,
    DateOnly? ClosedOn, string? ClosingObservations, int? EvidenceDocumentId,
    string ActorUserId, DateTime ChangedAtUtc);
public sealed record ProjectLifecycleStatusResponse(int ProjectId, ProjectStatus Status, long Version);
