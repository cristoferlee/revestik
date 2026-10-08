using Revestik.Shared.Expenses;

namespace Revestik.Api.Services.Expenses;

public interface IExpenseReportingService
{
    Task<ExpenseConsolidatedSummaryResponse> GetConsolidatedSummaryAsync(
        ExpenseSummaryRequest request,
        CancellationToken cancellationToken);
}
