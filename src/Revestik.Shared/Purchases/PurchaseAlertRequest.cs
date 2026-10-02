using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Purchases;

public sealed class PurchaseAlertRequest : IValidatableObject
{
    [Range(
        1,
        365,
        ErrorMessage = "La primera ventana debe estar entre 1 y 365 días.")]
    public int ShortWindowDays { get; set; } = 3;

    [Range(
        1,
        365,
        ErrorMessage = "La segunda ventana debe estar entre 1 y 365 días.")]
    public int LongWindowDays { get; set; } = 7;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (LongWindowDays < ShortWindowDays)
        {
            yield return new ValidationResult(
                "La segunda ventana no puede ser menor que la primera.",
                [nameof(LongWindowDays), nameof(ShortWindowDays)]);
        }
    }
}