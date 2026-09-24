using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Sales;

public sealed class SaleChargeRequest : IValidatableObject
{
    public SaleChargeType Type { get; set; }

    [StringLength(500, ErrorMessage = "La descripción del cargo no puede superar los 500 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999999999999.99", ErrorMessage = "El monto del cargo debe ser mayor que cero.")]
    public decimal Amount { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Type))
            yield return new ValidationResult("El tipo de cargo no es válido.", [nameof(Type)]);

        if (Type == SaleChargeType.Other && string.IsNullOrWhiteSpace(Description))
            yield return new ValidationResult("La descripción es obligatoria para cargos de tipo Otro.", [nameof(Description)]);

        if (decimal.Round(Amount, 2) != Amount)
            yield return new ValidationResult("El monto del cargo no puede tener más de 2 decimales.", [nameof(Amount)]);
    }
}