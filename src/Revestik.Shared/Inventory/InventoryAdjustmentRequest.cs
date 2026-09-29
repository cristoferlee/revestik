using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Inventory;

public sealed class InventoryAdjustmentRequest : IValidatableObject
{
    [Range(
        typeof(decimal),
        "0",
        "99999999999999.9999",
        ErrorMessage = "El nuevo inventario no puede ser negativo.")]
    public decimal NewStockQuantity { get; set; }

    public InventoryAdjustmentReason Reason { get; set; }

    [Required(ErrorMessage = "La nota es obligatoria.")]
    [StringLength(
        1000,
        MinimumLength = 3,
        ErrorMessage = "La nota debe contener entre 3 y 1000 caracteres.")]
    public string Notes { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Reason))
        {
            yield return new ValidationResult(
                "El motivo del ajuste no es válido.",
                [nameof(Reason)]);
        }

        if (string.IsNullOrWhiteSpace(Notes) ||
            Notes.Trim().Length < 3)
        {
            yield return new ValidationResult(
                "La nota debe contener al menos 3 caracteres.",
                [nameof(Notes)]);
        }
    }
}