namespace Revestik.Api.Models;

public sealed class Expense
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
