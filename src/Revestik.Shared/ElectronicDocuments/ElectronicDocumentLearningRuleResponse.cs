using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.ElectronicDocuments;

public static class ElectronicDocumentLearningRuleTypes
{
    public const string Issuer = "Issuer";
    public const string ExactCabys = "ExactCabys";
    public const string CabysBranch = "CabysBranch";
}

public sealed record ElectronicDocumentLearningRuleResponse(
    int Id,
    string RuleType,
    string IssuerIdentification,
    string? CabysCode,
    string? CabysCategory4Code,
    int CategoryId,
    string CategoryName,
    OperationalDestination? OperationalDestination,
    int ConfirmationCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed class ElectronicDocumentLearningRuleUpdateRequest : IValidatableObject
{
    [Required]
    public string RuleType { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int RuleId { get; set; }

    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    public OperationalDestination? OperationalDestination { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RuleType is not ElectronicDocumentLearningRuleTypes.Issuer
            and not ElectronicDocumentLearningRuleTypes.ExactCabys
            and not ElectronicDocumentLearningRuleTypes.CabysBranch)
        {
            yield return new ValidationResult(
                "El tipo de regla aprendida no es válido.",
                [nameof(RuleType)]);
        }

        if (RuleType != ElectronicDocumentLearningRuleTypes.Issuer &&
            !OperationalDestination.HasValue)
        {
            yield return new ValidationResult(
                "El destino operativo es obligatorio para reglas CAByS.",
                [nameof(OperationalDestination)]);
        }

        if (OperationalDestination.HasValue &&
            (!Enum.IsDefined(OperationalDestination.Value) ||
             OperationalDestination == Revestik.Shared.ElectronicDocuments.OperationalDestination.NoAction))
        {
            yield return new ValidationResult(
                "El destino operativo no es válido.",
                [nameof(OperationalDestination)]);
        }
    }
}
