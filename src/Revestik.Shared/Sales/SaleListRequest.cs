using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Sales;

public sealed class SaleListRequest : IValidatableObject
{
    [StringLength(
        150,
        ErrorMessage = "La búsqueda no puede superar los 150 caracteres.")]
    public string? Search { get; set; }

    public DateTime? DateFromUtc { get; set; }

    public DateTime? DateToUtc { get; set; }

    [EnumDataType(
        typeof(SaleStatus),
        ErrorMessage = "El estado de la venta no es válido.")]
    public SaleStatus? Status { get; set; }

    [EnumDataType(
        typeof(SaleBalanceStatus),
        ErrorMessage = "El estado de pago no es válido.")]
    public SaleBalanceStatus? BalanceStatus { get; set; }

    [EnumDataType(
        typeof(Currency),
        ErrorMessage = "La moneda no es válida.")]
    public Currency? Currency { get; set; }

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
        if (DateFromUtc.HasValue &&
            DateToUtc.HasValue &&
            DateFromUtc.Value > DateToUtc.Value)
        {
            yield return new ValidationResult(
                "La fecha inicial no puede ser posterior a la fecha final.",
                [nameof(DateFromUtc), nameof(DateToUtc)]);
        }
    }
}