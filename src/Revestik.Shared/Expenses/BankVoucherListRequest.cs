using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.Expenses;

public sealed class BankVoucherListRequest : IValidatableObject
{
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public BankVoucherStatus? Status { get; set; }
    public bool ExcludeNeedsReview { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (DateFrom.HasValue &&
            DateTo.HasValue &&
            DateFrom.Value > DateTo.Value)
        {
            yield return new ValidationResult(
                "La fecha inicial no puede ser posterior a la fecha final.",
                [nameof(DateFrom), nameof(DateTo)]);
        }

        if (Status.HasValue && ExcludeNeedsReview)
        {
            yield return new ValidationResult(
                "No se puede combinar un estado específico con el historial.",
                [nameof(Status), nameof(ExcludeNeedsReview)]);
        }
    }
}
