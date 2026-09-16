using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Quotations;

public sealed class QuotationLineRequest : IValidatableObject
{
    public int? ProductId { get; set; }

    [Required(ErrorMessage = "El código CABYS es obligatorio.")]
    [RegularExpression(
        "^\\d{13}$",
        ErrorMessage = "El código CABYS debe contener exactamente 13 dígitos.")]
    public string CabysCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(
        500,
        ErrorMessage = "La descripción no puede superar los 500 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Range(
        typeof(decimal),
        "0.00001",
        "9999999999999.99999",
        ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public decimal Quantity { get; set; }

    [Range(
        typeof(decimal),
        "0.00001",
        "9999999999999.99999",
        ErrorMessage = "El precio unitario debe ser mayor que cero.")]
    public decimal UnitPrice { get; set; }

    public DiscountType? DiscountType { get; set; }

    [Range(
        typeof(decimal),
        "0",
        "9999999999999.99999",
        ErrorMessage = "El descuento no puede ser negativo.")]
    public decimal DiscountValue { get; set; }

    public decimal TaxRate { get; set; } = 13m;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (TaxRate is not 0m and not 13m)
        {
            yield return new ValidationResult(
                "El IVA debe ser 0% o 13%.",
                [nameof(TaxRate)]);
        }

        if (DiscountType is null && DiscountValue != 0m)
        {
            yield return new ValidationResult(
                "Debe seleccionar un tipo de descuento cuando el descuento sea mayor que cero.",
                [nameof(DiscountType), nameof(DiscountValue)]);
        }

        if (DiscountType is not null && DiscountValue == 0m)
        {
            yield return new ValidationResult(
                "El valor del descuento debe ser mayor que cero cuando se selecciona un tipo de descuento.",
                [nameof(DiscountValue)]);
        }

        if (DiscountType == Quotations.DiscountType.Percentage &&
            DiscountValue > 100m)
        {
            yield return new ValidationResult(
                "El descuento porcentual no puede superar el 100%.",
                [nameof(DiscountValue)]);
        }

        if (DiscountType == Quotations.DiscountType.FixedAmount)
        {
            var grossAmount = Quantity * UnitPrice;

            if (DiscountValue > grossAmount)
            {
                yield return new ValidationResult(
                    "El descuento fijo no puede superar el importe de la línea.",
                    [nameof(DiscountValue)]);
            }
        }
    }
}