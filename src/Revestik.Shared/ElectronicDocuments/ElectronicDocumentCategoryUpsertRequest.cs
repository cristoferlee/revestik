using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.ElectronicDocuments;

public sealed class ElectronicDocumentCategoryUpsertRequest : IValidatableObject
{
    [Required(ErrorMessage = "El nombre de la categoría es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre de la categoría no puede superar los 100 caracteres.")]
    public string Name { get; set; } = string.Empty;

    public AccountingNature AccountingNature { get; set; } = AccountingNature.OperatingExpense;

    public OperationalDestination? DefaultOperationalDestination { get; set; }

    [Range(0, 10000, ErrorMessage = "El orden debe estar entre 0 y 10000.")]
    public int SortOrder { get; set; } = 100;

    public bool AllowsAutomaticSuggestion { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(AccountingNature))
        {
            yield return new ValidationResult(
                "La naturaleza contable no es válida.",
                [nameof(AccountingNature)]);
        }

        if (DefaultOperationalDestination.HasValue &&
            !Enum.IsDefined(DefaultOperationalDestination.Value))
        {
            yield return new ValidationResult(
                "El destino operativo sugerido no es válido.",
                [nameof(DefaultOperationalDestination)]);
        }

        if (DefaultOperationalDestination == OperationalDestination.NoAction)
        {
            yield return new ValidationResult(
                "Sin acción no puede configurarse como destino predeterminado de una categoría.",
                [nameof(DefaultOperationalDestination)]);
        }
    }
}
