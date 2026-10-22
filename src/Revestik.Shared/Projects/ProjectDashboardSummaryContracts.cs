namespace Revestik.Shared.Projects;

// Aggregated operational indicators, deliberately separate from Sales.
public sealed record ProjectDashboardCurrencySummaryResponse(
    string CurrencyCode,
    decimal AgreedAmount,
    decimal PaidAmount,
    decimal RemainingAmount);

public sealed record ProjectDashboardSummaryResponse(
    int TotalCount,
    int OpenCount,
    int CompletedCount,
    int CancelledCount,
    int ArchivedCount,
    int PlanningCount,
    int PendingApprovalCount,
    int ActiveCount,
    int PausedCount,
    int OpenProjectsWithoutAmount,
    int CompletedProjectsWithBalance,
    IReadOnlyList<ProjectDashboardCurrencySummaryResponse> OpenCurrencies);
