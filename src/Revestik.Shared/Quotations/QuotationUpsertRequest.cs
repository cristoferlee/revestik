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

    [MaxLength(
        2000,
        ErrorMessage = "Las observaciones no pueden exceder 2000 caracteres.")]
    public string Observations { get; set; } = string.Empty;

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

        for (var index = 0; index < Lines.Count; index++)
        {
            var validationResults =
                ValidateChild(Lines[index]);

            foreach (var validationResult in validationResults)
            {
                var memberNames =
                    validationResult.MemberNames
                        .DefaultIfEmpty("request")
                        .Select(memberName =>
                            $"{nameof(Lines)}[{index}].{memberName}");

                yield return new ValidationResult(
                    validationResult.ErrorMessage,
                    memberNames);
            }
        }

        for (var index = 0; index < Charges.Count; index++)
        {
            var validationResults =
                ValidateChild(Charges[index]);

            foreach (var validationResult in validationResults)
            {
                var memberNames =
                    validationResult.MemberNames
                        .DefaultIfEmpty("request")
                        .Select(memberName =>
                            $"{nameof(Charges)}[{index}].{memberName}");

                yield return new ValidationResult(
                    validationResult.ErrorMessage,
                    memberNames);
            }
        }
    }

    private static List<ValidationResult> ValidateChild<TRequest>(
        TRequest request)
        where TRequest : class
    {
        var validationResults =
            new List<ValidationResult>();

        var validationContext =
            new ValidationContext(request);

        Validator.TryValidateObject(
            request,
            validationContext,
            validationResults,
            validateAllProperties: true);

        return validationResults;
    }
}