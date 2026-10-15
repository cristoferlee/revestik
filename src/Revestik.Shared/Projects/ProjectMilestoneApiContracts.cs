namespace Revestik.Shared.Projects;

public sealed record ProjectMilestoneInputRequest(int? Id, string Name, string? Description,
    decimal Weight, int SortOrder);
public sealed record ConfigureProjectMilestonesRequest(
    IReadOnlyList<ProjectMilestoneInputRequest> Milestones, long? PlanVersion);
public sealed record ProjectMilestonePlanVersionRequest(long PlanVersion);
public sealed record SetProjectMilestoneCompletionRequest(
    bool Completed, long PlanVersion, long MilestoneVersion, string? Reason);

public sealed record ProjectMilestoneTemplateItemRequest(
    string Name, string? Description, decimal Weight, int SortOrder);
public sealed record CreateProjectMilestoneTemplateRequest(
    string Name, string? Description, IReadOnlyList<ProjectMilestoneTemplateItemRequest> Items);
public sealed record ProjectMilestoneTemplateItemResponse(
    int Id, string Name, string Description, decimal Weight, int SortOrder);
public sealed record ProjectMilestoneTemplateResponse(
    int Id, string Name, string Description, bool IsActive,
    IReadOnlyList<ProjectMilestoneTemplateItemResponse> Items);
public sealed record ApplyProjectMilestoneTemplateRequest(int TemplateId, long? PlanVersion);
