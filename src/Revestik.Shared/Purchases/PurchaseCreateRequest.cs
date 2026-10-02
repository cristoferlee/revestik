using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Purchases;

public sealed class PurchaseCreateRequest : IValidatableObject
{
    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "Debe seleccionar un proveedor.")]
    public int SupplierId { get; set; }

    [Required(ErrorMessage = "La fecha de compra es obligatoria.")]
    public DateOnly? PurchaseDate { get; set; }

    public PurchaseCurrency Currency { get; set; } = PurchaseCurrency.CRC;

    public decimal? ExchangeRate { get; set; }

    public PurchasePaymentType PaymentType { get; set; } =
        PurchasePaymentType.Cash;

    public int? CreditTermDays { get; set; }

    public PurchasePaymentRequest? InitialPayment { get; set; }

    [StringLength(
        2000,
        ErrorMessage = "Las notas no pueden superar los 2000 caracteres.")]
    public string Notes { get; set; } = string.Empty;

    [MinLength(
        1,
        ErrorMessage = "La compra debe contener al menos una línea.")]
    public List<PurchaseLineRequest> Lines { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Currency))
        {
            yield return new ValidationResult(
                "La moneda no es válida.",
                [nameof(Currency)]);
        }

        if (!Enum.IsDefined(PaymentType))
        {
            yield return new ValidationResult(
                "La condición de pago no es válida.",
                [nameof(PaymentType)]);
        }

        if (Currency == PurchaseCurrency.CRC && ExchangeRate is not null)
        {
            yield return new ValidationResult(
                "Las compras en colones no deben registrar tipo de cambio.",
                [nameof(ExchangeRate)]);
        }

        if (Currency == PurchaseCurrency.USD)
        {
            if (ExchangeRate is null or <= 0m)
            {
                yield return new ValidationResult(
                    "Debe registrar un tipo de cambio mayor que cero para una compra en dólares.",
                    [nameof(ExchangeRate)]);
            }
            else if (decimal.Round(ExchangeRate.Value, 6) != ExchangeRate.Value)
            {
                yield return new ValidationResult(
                    "El tipo de cambio no puede tener más de 6 decimales.",
                    [nameof(ExchangeRate)]);
            }
        }

        if (PaymentType == PurchasePaymentType.Cash)
        {
            if (CreditTermDays.HasValue)
            {
                yield return new ValidationResult(
                    "Una compra de contado no debe registrar plazo de crédito.",
                    [nameof(CreditTermDays)]);
            }

            if (InitialPayment is null)
            {
                yield return new ValidationResult(
                    "Una compra de contado debe registrar el pago inicial.",
                    [nameof(InitialPayment)]);
            }
        }
        else if (PaymentType == PurchasePaymentType.Credit)
        {
            if (CreditTermDays is null or <= 0)
            {
                yield return new ValidationResult(
                    "Una compra a crédito debe registrar un plazo mayor que cero.",
                    [nameof(CreditTermDays)]);
            }
        }

        if (InitialPayment is not null)
        {
            foreach (var result in ValidateChild(InitialPayment))
            {
                yield return Prefix(
                    result,
                    nameof(InitialPayment));
            }
        }

        var duplicateProductIds = Lines
            .Where(line => line.ProductId > 0)
            .GroupBy(line => line.ProductId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateProductIds.Length > 0)
        {
            yield return new ValidationResult(
                "Un producto no puede aparecer más de una vez en la misma compra.",
                [nameof(Lines)]);
        }

        for (var index = 0; index < Lines.Count; index++)
        {
            foreach (var result in ValidateChild(Lines[index]))
            {
                yield return Prefix(
                    result,
                    $"{nameof(Lines)}[{index}]");
            }
        }
    }

    private static IReadOnlyList<ValidationResult> ValidateChild<T>(
        T request)
        where T : class
    {
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        return results;
    }

    private static ValidationResult Prefix(
        ValidationResult result,
        string prefix)
    {
        var members = result.MemberNames
            .DefaultIfEmpty("request")
            .Select(name => $"{prefix}.{name}");

        return new ValidationResult(result.ErrorMessage, members);
    }
}