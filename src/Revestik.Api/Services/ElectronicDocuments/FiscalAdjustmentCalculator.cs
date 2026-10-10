using Revestik.Api.Models;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.ElectronicDocuments;

/// <summary>Pure documentary arithmetic; no database writes or economic operations.</summary>
public static class FiscalAdjustmentCalculator
{
    public sealed record Note(int Id, string Clave, ElectronicDocumentType Type,
        string CurrencyCode, decimal Amount);

    public static FiscalAdjustedTotal Calculate(int originalId, string currencyCode,
        decimal originalTotal, IEnumerable<Note> eligibleApprovedNotes)
    {
        ArgumentNullException.ThrowIfNull(eligibleApprovedNotes);
        if (string.IsNullOrWhiteSpace(currencyCode))
            throw new ArgumentException("La moneda del documento es obligatoria.", nameof(currencyCode));

        var notes = eligibleApprovedNotes.ToArray();
        var unique = new HashSet<int>();
        decimal credit = 0m, debit = 0m;
        var adjustments = new List<FiscalApprovedAdjustment>(notes.Length);
        foreach (var note in notes)
        {
            if (note.Id <= 0 || !unique.Add(note.Id))
                throw new InvalidOperationException("Ajuste fiscal duplicado o sin identificador válido.");
            if (!string.Equals(note.CurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("No se pueden mezclar monedas en un ajuste fiscal.");
            if (note.Amount < 0m)
                throw new InvalidOperationException("El importe de la nota fiscal no puede ser negativo.");
            decimal delta;
            switch (note.Type)
            {
                case ElectronicDocumentType.CreditNote:
                    credit = checked(credit + note.Amount);
                    delta = -note.Amount;
                    break;
                case ElectronicDocumentType.DebitNote:
                    debit = checked(debit + note.Amount);
                    delta = note.Amount;
                    break;
                default:
                    throw new InvalidOperationException("Solo se admiten notas de crédito y débito.");
            }
            adjustments.Add(new FiscalApprovedAdjustment(note.Id, note.Clave,
                note.Type.ToString(), delta));
        }
        var adjusted = checked(checked(originalTotal - credit) + debit);
        return new FiscalAdjustedTotal(originalId, currencyCode, originalTotal,
            credit, debit, adjusted, adjustments);
    }

    /// <summary>Projection of a pending note that is not already in the approved total.</summary>
    public static FiscalNoteAdjustmentPreview Project(FiscalAdjustedTotal approved,
        Note candidate)
    {
        ArgumentNullException.ThrowIfNull(approved);
        ArgumentNullException.ThrowIfNull(candidate);
        if (approved.Adjustments.Any(x => x.NoteId == candidate.Id))
            throw new InvalidOperationException("La nota ya está incluida en el total ajustado.");
        if (candidate.Id <= 0 || candidate.Amount < 0m)
            throw new InvalidOperationException("La nota tiene un identificador o importe inválido.");
        if (!string.Equals(approved.CurrencyCode, candidate.CurrencyCode,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La moneda de la nota y la factura no coincide.");
        var delta = candidate.Type switch
        {
            ElectronicDocumentType.CreditNote => -candidate.Amount,
            ElectronicDocumentType.DebitNote => candidate.Amount,
            _ => throw new InvalidOperationException("El documento no es una nota fiscal.")
        };
        var projected = checked(approved.AdjustedTotal + delta);
        return new FiscalNoteAdjustmentPreview(approved.DocumentId, approved.CurrencyCode,
            approved.OriginalTotal, delta, projected,
            "Proyección fiscal documental. No modifica saldos ni movimientos económicos.")
        {
            CurrentAdjustedTotal = approved.AdjustedTotal,
            ApprovedCreditNotesTotal = approved.CreditNotesTotal,
            ApprovedDebitNotesTotal = approved.DebitNotesTotal,
            PotentialExcess = projected < 0m ? -projected : 0m
        };
    }
}
