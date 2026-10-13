namespace Revestik.Shared.Projects;

public sealed record ProjectEventCategoryCreateRequest(string Name);
public sealed record ProjectEventCategoryResponse(int Id, string Name, bool IsActive);
public sealed record ProjectEventUpsertRequest(int CategoryId, string Title, string? Description, DateOnly OccurredOn, long? Version = null);
public sealed record ProjectEventVoidRequest(long Version, string Reason);
public sealed record ProjectEventResponse(int Id, int ProjectId, int CategoryId, string CategoryName,
    string Title, string Description, DateOnly OccurredOn, string CreatedByUserId, DateTime CreatedAtUtc,
    string? UpdatedByUserId, DateTime? UpdatedAtUtc, bool IsVoided, string? VoidReason, long Version);
public sealed record ProjectEventSearchRequest(int? CategoryId = null, DateOnly? DateFrom = null,
    DateOnly? DateTo = null, string? CreatedByUserId = null, bool IncludeVoided = false);
