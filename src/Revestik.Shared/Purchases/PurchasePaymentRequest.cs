using System.ComponentModel.DataAnnotations;
using Revestik.Shared.Sales;

namespace Revestik.Shared.Purchases;

public sealed class PurchasePaymentRequest : IValidatableObject
{
    [Range(
        typeof(decimal),
        "0.01",
        "9999999999999.99",
        ErrorMessage = "El monto del pago debe ser mayor que cero.")]
    public decimal Amount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public DateTime PaidAtUtc { get; set; }

    public decimal? ExchangeRate { get; set; }

    [StringLength(
        200,
        ErrorMessage = "La referencia no puede superar los 200 caracteres.")]
    public string Reference { get; set; } = string.Empty;

    [StringLength(
        1000,
        ErrorMessage = "Las notas no pueden superar los 1000 caracteres.")]
    public string Notes { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (!Enum.IsDefined(PaymentMethod))
        {
            yield return new ValidationResult(
                "El método de pago no es válido.",
                [nameof(PaymentMethod)]);
        }

        if (PaidAtUtc == default)
        {
            yield return new ValidationResult(
                "La fecha del pago es obligatoria.",
                [nameof(PaidAtUtc)]);
        }

        if (decimal.Round(Amount, 2) != Amount)
        {
            yield return new ValidationResult(
                "El monto del pago no puede tener más de 2 decimales.",
                [nameof(Amount)]);
        }

        if (ExchangeRate is <= 0m)
        {
            yield return new ValidationResult(
                "El tipo de cambio del pago debe ser mayor que cero.",
                [nameof(ExchangeRate)]);
        }

        if (ExchangeRate.HasValue &&
            decimal.Round(ExchangeRate.Value, 6) != ExchangeRate.Value)
        {
            yield return new ValidationResult(
                "El tipo de cambio del pago no puede tener más de 6 decimales.",
                [nameof(ExchangeRate)]);
        }
    }
}