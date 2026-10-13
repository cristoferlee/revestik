namespace Revestik.Shared.Projects;

public sealed record ProjectCommentCreateRequest(string Text);
public sealed record ProjectCommentResponse(long Id, int ProjectEventId, string Text,
    string CreatedByUserId, DateTime CreatedAtUtc);

public enum ProjectAgreementStatus { Pending, Fulfilled, Modified, Unfulfilled }

public sealed record ProjectAgreementUpsertRequest(ProjectAgreementStatus Status, string? AcceptedBy,
    DateOnly? AcceptedOn, string? AcceptanceMedium, string? Notes, long? Version = null);
public sealed record ProjectAgreementResponse(int ProjectEventId, ProjectAgreementStatus Status,
    string? AcceptedBy, DateOnly? AcceptedOn, string? AcceptanceMedium, string? Notes,
    long Version, DateTime UpdatedAtUtc, string UpdatedByUserId);

public sealed record ProjectEventRevisionResponse(long Id, long PreviousVersion, int CategoryId,
    string Title, string Description, DateOnly OccurredOn, bool WasVoided, string? PreviousVoidReason,
    string ChangedByUserId, DateTime ChangedAtUtc, string ChangeKind);
public sealed record ProjectAgreementRevisionResponse(long Id, long PreviousVersion, ProjectAgreementStatus Status,
    string? AcceptedBy, DateOnly? AcceptedOn, string? AcceptanceMedium, string? Notes,
    string ChangedByUserId, DateTime ChangedAtUtc);
