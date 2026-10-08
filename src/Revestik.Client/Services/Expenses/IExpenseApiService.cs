using Revestik.Shared.Common;
using Revestik.Shared.Expenses;

namespace Revestik.Client.Services.Expenses;

public interface IExpenseApiService
{
    Task<PaginatedResponse<ExpenseListItemResponse>> GetPageAsync(
        ExpenseListRequest request,
        CancellationToken cancellationToken = default);

    Task<ExpenseSummaryResponse> GetSummaryAsync(
        ExpenseSummaryRequest request,
        CancellationToken cancellationToken = default);

    Task<ExpenseConsolidatedSummaryResponse> GetConsolidatedSummaryAsync(
        ExpenseSummaryRequest request,
        CancellationToken cancellationToken = default);

    Task<ExpenseResponse> CreateAsync(
        ExpenseCreateRequest request,
        CancellationToken cancellationToken = default);
}
