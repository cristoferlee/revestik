using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Quotations;

public sealed class QuotationChargeRequest : IValidatableObject
{
    public QuotationChargeType Type { get; set; }

    [StringLength(
        500,
        ErrorMessage = "La descripción del cargo no puede superar los 500 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Range(
        typeof(decimal),
        "0.01",
        "9999999999999.99",
        ErrorMessage = "El monto del cargo debe ser mayor que cero.")]
    public decimal Amount { get; set; }

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Type))
        {
            yield return new ValidationResult(
                "El tipo de cargo no es válido.",
                [nameof(Type)]);
        }

        if (Type == QuotationChargeType.Other &&
            string.IsNullOrWhiteSpace(Description))
        {
            yield return new ValidationResult(
                "La descripción es obligatoria para cargos de tipo Otro.",
                [nameof(Description)]);
        }

        if (HasMoreThanTwoDecimalPlaces(Amount))
        {
            yield return new ValidationResult(
                "El monto del cargo no puede tener más de 2 decimales.",
                [nameof(Amount)]);
        }
    }

    private static bool HasMoreThanTwoDecimalPlaces(decimal value)
    {
        return decimal.Round(value, 2) != value;
    }
}