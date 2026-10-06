using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.ElectronicDocuments;

public sealed class ElectronicDocumentSummaryRequest : IValidatableObject
{
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DateFrom.HasValue && DateTo.HasValue && DateFrom.Value > DateTo.Value)
        {
            yield return new ValidationResult(
                "La fecha inicial no puede ser posterior a la fecha final.",
                [nameof(DateFrom), nameof(DateTo)]);
        }
    }
}
