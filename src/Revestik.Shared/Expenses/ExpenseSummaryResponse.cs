namespace Revestik.Shared.Expenses;

public sealed record ExpenseSummaryResponse(
    int ExpenseCount,
    decimal TotalAmount);
