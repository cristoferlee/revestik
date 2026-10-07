using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Common;
using Revestik.Shared.Expenses;

namespace Revestik.Api.Services.Expenses;

public sealed class ExpenseService(RevestikDbContext dbContext)
    : IExpenseService
{
    public async Task<PaginatedResponse<ExpenseListItemResponse>> GetPageAsync(
        ExpenseListRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = ApplyDateRange(
            dbContext.Expenses.AsNoTracking(),
            request.DateFrom,
            request.DateTo);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(expense =>
                expense.Name.Contains(search) ||
                expense.Description.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var skip = ((long)request.Page - 1) * request.PageSize;

        IReadOnlyList<ExpenseListItemResponse> items;

        if (skip >= totalCount)
        {
            items = [];
        }
        else
        {
            items = await query
                .OrderByDescending(expense => expense.ExpenseDate)
                .ThenByDescending(expense => expense.Id)
                .Skip((int)skip)
                .Take(request.PageSize)
                .Select(expense => new ExpenseListItemResponse(
                    expense.Id,
                    expense.Name,
                    expense.Description,
                    expense.TotalAmount,
                    expense.ExpenseDate,
                    expense.CreatedAtUtc))
                .ToListAsync(cancellationToken);
        }

        return new PaginatedResponse<ExpenseListItemResponse>(
            items,
            request.Page,
            request.PageSize,
            totalCount);
    }

    public async Task<ExpenseSummaryResponse> GetSummaryAsync(
        ExpenseSummaryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = ApplyDateRange(
            dbContext.Expenses.AsNoTracking(),
            request.DateFrom,
            request.DateTo);

        var expenseCount = await query.CountAsync(cancellationToken);
        var totalAmount = await query
            .Select(expense => (decimal?)expense.TotalAmount)
            .SumAsync(cancellationToken) ?? 0m;

        return new ExpenseSummaryResponse(
            expenseCount,
            totalAmount);
    }

    public async Task<ExpenseResponse> CreateAsync(
        ExpenseCreateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = NormalizeRequired(request.Name, nameof(request.Name));
        var description = NormalizeRequired(
            request.Description,
            nameof(request.Description));

        if (request.TotalAmount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.TotalAmount),
                "The expense total must be greater than zero.");
        }

        if (!request.ExpenseDate.HasValue)
        {
            throw new ArgumentException(
                "The expense date is required.",
                nameof(request.ExpenseDate));
        }

        var expense = new Expense
        {
            Name = name,
            Description = description,
            TotalAmount = request.TotalAmount,
            ExpenseDate = request.ExpenseDate.Value,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(expense);
    }

    private static IQueryable<Expense> ApplyDateRange(
        IQueryable<Expense> query,
        DateOnly? dateFrom,
        DateOnly? dateTo)
    {
        if (dateFrom.HasValue)
        {
            query = query.Where(expense =>
                expense.ExpenseDate >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(expense =>
                expense.ExpenseDate <= dateTo.Value);
        }

        return query;
    }

    private static ExpenseResponse MapToResponse(Expense expense) =>
        new(
            expense.Id,
            expense.Name,
            expense.Description,
            expense.TotalAmount,
            expense.ExpenseDate,
            expense.CreatedAtUtc);

    private static string NormalizeRequired(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "A required value cannot be empty.",
                parameterName);
        }

        return value.Trim();
    }
}
