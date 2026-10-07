namespace Revestik.Shared.Expenses;

public sealed record ExpenseResponse(
    int Id,
    string Name,
    string Description,
    decimal TotalAmount,
    DateOnly ExpenseDate,
    DateTime CreatedAtUtc);
