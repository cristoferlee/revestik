using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Quotations;

public sealed class QuotationUpsertRequest : IValidatableObject
{
    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "Debe seleccionar un cliente.")]
    public int CustomerId { get; set; }

    public Currency Currency { get; set; } = Currency.CRC;

    public DateTime? ValidUntilUtc { get; set; }

    [MinLength(
        1,
        ErrorMessage = "La cotización debe contener al menos una línea.")]
    public List<QuotationLineRequest> Lines { get; set; } = [];

    public List<QuotationChargeRequest> Charges { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Currency))
        {
            yield return new ValidationResult(
                "La moneda no es válida.",
                [nameof(Currency)]);
        }

        if (ValidUntilUtc.HasValue &&
            ValidUntilUtc.Value <= DateTime.UtcNow)
        {
            yield return new ValidationResult(
                "La fecha de validez debe ser posterior a la fecha actual.",
                [nameof(ValidUntilUtc)]);
        }
    }
}