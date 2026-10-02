using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Purchases;

public sealed class PurchaseListRequest : IValidatableObject
{
    public int? SupplierId { get; set; }

    public DateOnly? DateFrom { get; set; }

    public DateOnly? DateTo { get; set; }

    public PurchaseCurrency? Currency { get; set; }

    public PurchasePaymentType? PaymentType { get; set; }

    public PurchaseBalanceStatus? BalanceStatus { get; set; }

    public bool? OverdueOnly { get; set; }

    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "El número de página debe ser mayor que cero.")]
    public int Page { get; set; } = 1;

    [Range(
        1,
        100,
        ErrorMessage = "El tamaño de página debe estar entre 1 y 100.")]
    public int PageSize { get; set; } = 20;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (SupplierId is <= 0)
        {
            yield return new ValidationResult(
                "El proveedor no es válido.",
                [nameof(SupplierId)]);
        }

        if (DateFrom.HasValue &&
            DateTo.HasValue &&
            DateFrom.Value > DateTo.Value)
        {
            yield return new ValidationResult(
                "La fecha inicial no puede ser posterior a la fecha final.",
                [nameof(DateFrom), nameof(DateTo)]);
        }

        if (Currency.HasValue &&
            !Enum.IsDefined(Currency.Value))
        {
            yield return new ValidationResult(
                "La moneda no es válida.",
                [nameof(Currency)]);
        }

        if (PaymentType.HasValue &&
            !Enum.IsDefined(PaymentType.Value))
        {
            yield return new ValidationResult(
                "La condición de pago no es válida.",
                [nameof(PaymentType)]);
        }

        if (BalanceStatus.HasValue &&
            !Enum.IsDefined(BalanceStatus.Value))
        {
            yield return new ValidationResult(
                "El estado de saldo no es válido.",
                [nameof(BalanceStatus)]);
        }
    }
}