using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Sales;

public sealed class SaleLineRequest : IValidatableObject
{
    public int? ProductId { get; set; }
    public string CabysCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(500, ErrorMessage = "La descripción no puede superar los 500 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "La unidad de medida es obligatoria.")]
    [StringLength(50, ErrorMessage = "La unidad de medida no puede superar los 50 caracteres.")]
    public string Unit { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999999999999.99", ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public decimal Quantity { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999.99", ErrorMessage = "El precio unitario debe ser mayor que cero.")]
    public decimal UnitPrice { get; set; }

    public DiscountType? DiscountType { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ErrorMessage = "El descuento no puede ser negativo.")]
    public decimal DiscountValue { get; set; }

    public decimal TaxRate { get; set; } = 13m;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(CabysCode) && !IsValidCabysCode(CabysCode))
            yield return new ValidationResult("El código CABYS debe contener exactamente 13 dígitos.", [nameof(CabysCode)]);

        if (HasMoreThanTwoDecimalPlaces(Quantity))
            yield return new ValidationResult("La cantidad no puede tener más de 2 decimales.", [nameof(Quantity)]);

        if (HasMoreThanTwoDecimalPlaces(UnitPrice))
            yield return new ValidationResult("El precio unitario no puede tener más de 2 decimales.", [nameof(UnitPrice)]);

        if (HasMoreThanTwoDecimalPlaces(DiscountValue))
            yield return new ValidationResult("El descuento no puede tener más de 2 decimales.", [nameof(DiscountValue)]);

        if (TaxRate is not 0m and not 13m)
            yield return new ValidationResult("El IVA debe ser 0% o 13%.", [nameof(TaxRate)]);

        if (DiscountType is null && DiscountValue != 0m)
            yield return new ValidationResult("Debe seleccionar un tipo de descuento cuando el descuento sea mayor que cero.", [nameof(DiscountType), nameof(DiscountValue)]);

        if (DiscountType is not null && DiscountValue == 0m)
            yield return new ValidationResult("El valor del descuento debe ser mayor que cero cuando se selecciona un tipo de descuento.", [nameof(DiscountValue)]);

        if (DiscountType == Sales.DiscountType.Percentage && DiscountValue > 100m)
            yield return new ValidationResult("El descuento porcentual no puede superar el 100%.", [nameof(DiscountValue)]);

        if (DiscountType == Sales.DiscountType.FixedAmount && DiscountValue > Quantity * UnitPrice)
            yield return new ValidationResult("El descuento fijo no puede superar el importe de la línea.", [nameof(DiscountValue)]);
    }

    private static bool IsValidCabysCode(string cabysCode)
    {
        var normalized = cabysCode.Trim();
        return normalized.Length == 13 && normalized.All(char.IsDigit);
    }

    private static bool HasMoreThanTwoDecimalPlaces(decimal value) => decimal.Round(value, 2) != value;
}