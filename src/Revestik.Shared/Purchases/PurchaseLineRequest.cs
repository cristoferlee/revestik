using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Purchases;

public sealed class PurchaseLineRequest : IValidatableObject
{
    public int ProductId { get; set; }

    public PurchaseNewProductRequest? NewProduct { get; set; }

    [Range(typeof(decimal), "0.0001", "9999999999999.9999", ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public decimal Quantity { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999.99", ErrorMessage = "El costo unitario debe ser mayor que cero.")]
    public decimal UnitCost { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ProductId <= 0 && NewProduct is null)
            yield return new ValidationResult("Debe seleccionar un producto o completar uno nuevo.", [nameof(ProductId), nameof(NewProduct)]);

        if (ProductId > 0 && NewProduct is not null)
            yield return new ValidationResult("La línea no puede referenciar un producto existente y uno nuevo al mismo tiempo.", [nameof(ProductId), nameof(NewProduct)]);

        if (NewProduct is not null)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(NewProduct, new ValidationContext(NewProduct), results, true);
            foreach (var result in results)
            {
                var members = result.MemberNames.DefaultIfEmpty("request").Select(name => $"{nameof(NewProduct)}.{name}");
                yield return new ValidationResult(result.ErrorMessage, members);
            }
        }

        if (decimal.Round(Quantity, 4) != Quantity)
            yield return new ValidationResult("La cantidad no puede tener más de 4 decimales.", [nameof(Quantity)]);

        if (decimal.Round(UnitCost, 2) != UnitCost)
            yield return new ValidationResult("El costo unitario no puede tener más de 2 decimales.", [nameof(UnitCost)]);
    }
}