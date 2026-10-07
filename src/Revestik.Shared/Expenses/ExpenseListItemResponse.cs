namespace Revestik.Shared.Expenses;

public sealed record ExpenseListItemResponse(
    int Id,
    string Name,
    string Description,
    decimal TotalAmount,
    DateOnly ExpenseDate,
    DateTime CreatedAtUtc);
