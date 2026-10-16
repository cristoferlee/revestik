namespace Revestik.Shared.Projects;
public sealed record ProjectDashboardAlertResponse(int ProjectId, string ProjectName, string Kind,
    int SourceId, string Title, DateOnly DueOn, bool IsOverdue);
