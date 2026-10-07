using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Expenses;

public sealed class ExpenseCreateRequest
{
    [Required(ErrorMessage = "El nombre del gasto es obligatorio.")]
    [StringLength(
        150,
        ErrorMessage = "El nombre del gasto no puede superar los 150 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción del gasto es obligatoria.")]
    [StringLength(
        1000,
        ErrorMessage = "La descripción del gasto no puede superar los 1000 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Range(
        typeof(decimal),
        "0.01",
        "9999999999999.99",
        ErrorMessage = "El total del gasto debe ser mayor que cero.")]
    public decimal TotalAmount { get; set; }

    [Required(ErrorMessage = "La fecha del gasto es obligatoria.")]
    public DateOnly? ExpenseDate { get; set; }
}
