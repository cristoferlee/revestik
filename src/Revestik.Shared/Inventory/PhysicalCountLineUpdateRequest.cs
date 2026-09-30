using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Inventory;

public sealed class PhysicalCountLineUpdateRequest : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "La línea del conteo es obligatoria.")]
    public int LineId { get; set; }

    [Range(
        typeof(decimal),
        "0",
        "99999999999999.9999",
        ErrorMessage = "La cantidad contada no puede ser negativa.")]
    public decimal CountedQuantity { get; set; }

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (decimal.Round(CountedQuantity, 4) != CountedQuantity)
        {
            yield return new ValidationResult(
                "La cantidad contada no puede tener más de 4 decimales.",
                [nameof(CountedQuantity)]);
        }
    }
}