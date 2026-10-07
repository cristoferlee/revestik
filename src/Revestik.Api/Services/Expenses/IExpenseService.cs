using Revestik.Shared.Common;
using Revestik.Shared.Expenses;

namespace Revestik.Api.Services.Expenses;

public interface IExpenseService
{
    Task<PaginatedResponse<ExpenseListItemResponse>> GetPageAsync(
        ExpenseListRequest request,
        CancellationToken cancellationToken);

    Task<ExpenseSummaryResponse> GetSummaryAsync(
        ExpenseSummaryRequest request,
        CancellationToken cancellationToken);

    Task<ExpenseResponse> CreateAsync(
        ExpenseCreateRequest request,
        CancellationToken cancellationToken);
}
