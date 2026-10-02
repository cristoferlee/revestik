using System.ComponentModel.DataAnnotations;
using Revestik.Shared.Products;

namespace Revestik.Shared.Purchases;

public sealed class PurchaseNewProductRequest : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "La categoría es obligatoria.")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(150, ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "La descripción no puede superar los 500 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "El código CABYS es obligatorio.")]
    [RegularExpression("^\\d{13}$", ErrorMessage = "El código CABYS debe contener exactamente 13 dígitos.")]
    public string CabysCode { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "La unidad física es obligatoria.")]
    public int InventoryUnitId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La unidad de contenido es obligatoria.")]
    public int CommercialUnitId { get; set; }

    [Range(typeof(decimal), "0.0001", "99999999999999.9999", ErrorMessage = "La conversión debe ser mayor que cero.")]
    public decimal CommercialUnitsPerInventoryUnit { get; set; } = 1m;

    public bool RequiresWholeInventoryUnits { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999.99", ErrorMessage = "El precio de venta debe ser mayor que cero.")]
    public decimal SalePrice { get; set; }

    public SalePriceBasis SalePriceBasis { get; set; } = SalePriceBasis.InventoryUnit;

    public decimal TaxRate { get; set; } = 13m;

    [Range(typeof(decimal), "0", "99999999999999.9999", ErrorMessage = "El inventario mínimo no puede ser negativo.")]
    public decimal MinimumStock { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(SalePriceBasis))
            yield return new ValidationResult("La unidad del precio no es válida.", [nameof(SalePriceBasis)]);

        if (TaxRate is not 0m and not 13m)
            yield return new ValidationResult("El IVA debe ser 0% o 13%.", [nameof(TaxRate)]);

        if (InventoryUnitId == CommercialUnitId && CommercialUnitsPerInventoryUnit != 1m)
            yield return new ValidationResult("La conversión debe ser 1 cuando ambas unidades son iguales.", [nameof(CommercialUnitsPerInventoryUnit)]);

        if (InventoryUnitId == CommercialUnitId && SalePriceBasis != SalePriceBasis.InventoryUnit)
            yield return new ValidationResult("Cuando ambas unidades son iguales, el precio corresponde a la unidad física.", [nameof(SalePriceBasis)]);
    }
}