using System.ComponentModel.DataAnnotations;

namespace Revestik.Shared.ElectronicDocuments;

public sealed class ElectronicDocumentLineClassificationRequest : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "La línea es obligatoria.")]
    public int LineId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La categoría de la línea es obligatoria.")]
    public int CategoryId { get; set; }

    public OperationalDestination OperationalDestination { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(OperationalDestination) ||
            OperationalDestination == Revestik.Shared.ElectronicDocuments.OperationalDestination.NoAction)
        {
            yield return new ValidationResult(
                "El destino operativo de la línea no es válido.",
                [nameof(OperationalDestination)]);
        }
    }
}

public sealed class ElectronicDocumentClassifyRequest : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "La categoría es obligatoria.")]
    public int CategoryId { get; set; }

    public OperationalDestination? OperationalDestination { get; set; }

    public bool SeparateByLine { get; set; }

    public List<ElectronicDocumentLineClassificationRequest> LineClassifications { get; set; } = [];

    public ElectronicDocumentProcessingStatus ProcessingStatus { get; set; } =
        ElectronicDocumentProcessingStatus.Processed;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ProcessingStatus is not ElectronicDocumentProcessingStatus.Processed
            and not ElectronicDocumentProcessingStatus.NoActionRequired)
        {
            yield return new ValidationResult(
                "El estado final debe ser Procesado o Sin acción requerida.",
                [nameof(ProcessingStatus)]);
        }

        if (ProcessingStatus == ElectronicDocumentProcessingStatus.Processed &&
            !OperationalDestination.HasValue)
        {
            yield return new ValidationResult(
                "El destino operativo es obligatorio para procesar el documento.",
                [nameof(OperationalDestination)]);
        }

        if (ProcessingStatus == ElectronicDocumentProcessingStatus.Processed &&
            OperationalDestination == Revestik.Shared.ElectronicDocuments.OperationalDestination.NoAction)
        {
            yield return new ValidationResult(
                "Sin acción no es un destino operativo válido para un documento procesado.",
                [nameof(OperationalDestination)]);
        }

        if (OperationalDestination.HasValue &&
            !Enum.IsDefined(OperationalDestination.Value))
        {
            yield return new ValidationResult(
                "El destino operativo no es válido.",
                [nameof(OperationalDestination)]);
        }

        if (!SeparateByLine && LineClassifications.Count > 0)
        {
            yield return new ValidationResult(
                "No se pueden enviar clasificaciones por línea si la separación por líneas está desactivada.",
                [nameof(LineClassifications)]);
        }

        if (ProcessingStatus == ElectronicDocumentProcessingStatus.NoActionRequired &&
            LineClassifications.Count > 0)
        {
            yield return new ValidationResult(
                "Un documento sin acción requerida no debe tener clasificaciones operativas por línea.",
                [nameof(LineClassifications)]);
        }

        foreach (var line in LineClassifications)
        {
            if (line.LineId <= 0)
            {
                yield return new ValidationResult(
                    "La línea es obligatoria.",
                    [nameof(LineClassifications)]);
            }

            if (line.CategoryId <= 0)
            {
                yield return new ValidationResult(
                    "La categoría de cada línea es obligatoria.",
                    [nameof(LineClassifications)]);
            }

            if (!Enum.IsDefined(line.OperationalDestination) ||
                line.OperationalDestination == Revestik.Shared.ElectronicDocuments.OperationalDestination.NoAction)
            {
                yield return new ValidationResult(
                    "El destino operativo de cada línea debe ser válido.",
                    [nameof(LineClassifications)]);
            }
        }

        var duplicateLineIds = LineClassifications
            .GroupBy(x => x.LineId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateLineIds.Length > 0)
        {
            yield return new ValidationResult(
                "Cada línea fiscal puede clasificarse una sola vez.",
                [nameof(LineClassifications)]);
        }
    }
}
