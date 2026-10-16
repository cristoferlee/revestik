namespace Revestik.Shared.Projects;

public sealed record ProjectTaskCreateRequest(string Title, string? Description,
    string? AssignedToUserId, DateOnly? DueOn);
public sealed record ProjectTaskUpdateRequest(string Title, string? Description,
    string? AssignedToUserId, DateOnly? DueOn, bool IsCompleted, long Version, string Reason);
public sealed record ProjectTaskResponse(int Id, int ProjectId, string Title, string Description,
    string? AssignedToUserId, DateOnly? DueOn, bool IsCompleted, DateTime? CompletedAtUtc,
    string? CompletedByUserId, long Version, string CreatedByUserId, DateTime CreatedAtUtc);
public sealed record ProjectTaskRevisionResponse(long PreviousVersion, string Title, string Description,
    string? AssignedToUserId, DateOnly? DueOn, bool WasCompleted, string ChangedByUserId,
    DateTime ChangedAtUtc, string Reason);
public sealed record ProjectEventDeadlineSetRequest(DateOnly? DueOn, long? Version, string Reason);
public sealed record ProjectEventDeadlineResponse(int ProjectEventId, DateOnly? DueOn, long? Version,
    string? ChangedByUserId, DateTime? ChangedAtUtc);
public sealed record ProjectEventDeadlineRevisionResponse(long PreviousVersion, DateOnly? PreviousDueOn,
    string ChangedByUserId, DateTime ChangedAtUtc, string Reason);
