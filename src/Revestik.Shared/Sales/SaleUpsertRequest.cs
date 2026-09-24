using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Sales;

public sealed class SaleUpsertRequest : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un cliente.")]
    public int CustomerId { get; set; }

    public Currency Currency { get; set; } = Currency.CRC;
    public DiscountType? GeneralDiscountType { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ErrorMessage = "El descuento general no puede ser negativo.")]
    public decimal GeneralDiscountValue { get; set; }

    [MaxLength(2000, ErrorMessage = "Las observaciones no pueden exceder 2000 caracteres.")]
    public string Observations { get; set; } = string.Empty;

    [MinLength(1, ErrorMessage = "La venta debe contener al menos una línea.")]
    public List<SaleLineRequest> Lines { get; set; } = [];
    public List<SaleChargeRequest> Charges { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Currency))
            yield return new ValidationResult("La moneda no es válida.", [nameof(Currency)]);

        if (GeneralDiscountType is null && GeneralDiscountValue != 0m)
            yield return new ValidationResult("Debe seleccionar un tipo de descuento general cuando el descuento sea mayor que cero.", [nameof(GeneralDiscountType), nameof(GeneralDiscountValue)]);

        if (GeneralDiscountType is not null && GeneralDiscountValue == 0m)
            yield return new ValidationResult("El valor del descuento general debe ser mayor que cero cuando se selecciona un tipo.", [nameof(GeneralDiscountValue)]);

        if (GeneralDiscountType == DiscountType.Percentage && GeneralDiscountValue > 100m)
            yield return new ValidationResult("El descuento general porcentual no puede superar el 100%.", [nameof(GeneralDiscountValue)]);

        if (decimal.Round(GeneralDiscountValue, 2) != GeneralDiscountValue)
            yield return new ValidationResult("El descuento general no puede tener más de 2 decimales.", [nameof(GeneralDiscountValue)]);

        for (var i = 0; i < Lines.Count; i++)
            foreach (var result in ValidateChild(Lines[i]))
                yield return Prefix(result, $"{nameof(Lines)}[{i}]");

        for (var i = 0; i < Charges.Count; i++)
            foreach (var result in ValidateChild(Charges[i]))
                yield return Prefix(result, $"{nameof(Charges)}[{i}]");
    }

    private static List<ValidationResult> ValidateChild<T>(T request) where T : class
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, true);
        return results;
    }

    private static ValidationResult Prefix(ValidationResult result, string prefix)
    {
        var members = result.MemberNames.DefaultIfEmpty("request").Select(name => $"{prefix}.{name}");
        return new ValidationResult(result.ErrorMessage, members);
    }
}