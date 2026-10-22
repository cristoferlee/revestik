namespace Revestik.Shared.Dashboard;

public sealed record DashboardRecentActivityItemResponse(
    string Kind,
    string Title,
    string Description,
    DateTime OccurredAtUtc,
    string Path);

public sealed record DashboardRecentActivityResponse(
    IReadOnlyList<DashboardRecentActivityItemResponse> Items);
