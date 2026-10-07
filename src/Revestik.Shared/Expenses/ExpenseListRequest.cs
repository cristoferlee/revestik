using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Expenses;

public sealed class ExpenseListRequest : IValidatableObject
{
    [StringLength(
        150,
        ErrorMessage = "La búsqueda no puede superar los 150 caracteres.")]
    public string? Search { get; set; }

    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }

    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "El número de página debe ser mayor que cero.")]
    public int Page { get; set; } = 1;

    [Range(
        1,
        100,
        ErrorMessage = "El tamaño de página debe estar entre 1 y 100.")]
    public int PageSize { get; set; } = 20;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (DateFrom.HasValue &&
            DateTo.HasValue &&
            DateFrom.Value > DateTo.Value)
        {
            yield return new ValidationResult(
                "La fecha inicial no puede ser posterior a la fecha final.",
                [nameof(DateFrom), nameof(DateTo)]);
        }
    }
}
