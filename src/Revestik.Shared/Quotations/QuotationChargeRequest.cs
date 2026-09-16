using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Quotations;

public sealed class QuotationChargeRequest : IValidatableObject
{
    public QuotationChargeType Type { get; set; }

    [Required(ErrorMessage = "La descripción del cargo es obligatoria.")]
    [StringLength(
        500,
        ErrorMessage = "La descripción del cargo no puede superar los 500 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Range(
        typeof(decimal),
        "0.00001",
        "9999999999999.99999",
        ErrorMessage = "El monto del cargo debe ser mayor que cero.")]
    public decimal Amount { get; set; }

    public decimal TaxRate { get; set; } = 13m;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Type))
        {
            yield return new ValidationResult(
                "El tipo de cargo no es válido.",
                [nameof(Type)]);
        }

        if (TaxRate is not 0m and not 13m)
        {
            yield return new ValidationResult(
                "El IVA debe ser 0% o 13%.",
                [nameof(TaxRate)]);
        }
    }
}