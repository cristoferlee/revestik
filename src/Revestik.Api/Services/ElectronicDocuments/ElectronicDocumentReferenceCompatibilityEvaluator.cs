using Revestik.Api.Models;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Services.ElectronicDocuments;

/// <summary>
/// Pure compatibility assessment of an imported fiscal reference. This evaluates a
/// relationship only: it does not admit an XML from quarantine, approve a note, or
/// authorize inclusion in period totals.
/// </summary>
public static class ElectronicDocumentReferenceCompatibilityEvaluator
{
    // Stable, machine-readable result. Display messages remain backward compatible.
    public enum CompatibilityIssue
    {
        SelfReference, Direction, Issuer, Receiver, ReferenceNumber, DocumentType,
        IssueDate, Currency
    }

    public sealed record CompatibilityFinding(CompatibilityIssue Code, string Message);

    public static IReadOnlyList<string> Evaluate(
        ElectronicDocument note, ElectronicDocumentReference reference,
        ElectronicDocument original) =>
        EvaluateDetailed(note, reference, original).Select(x => x.Message).ToArray();

    public static IReadOnlyList<CompatibilityFinding> EvaluateDetailed(
        ElectronicDocument note, ElectronicDocumentReference reference,
        ElectronicDocument original)
    {
        var reasons = new List<CompatibilityFinding>();
        if (note.Id == original.Id)
            reasons.Add(new CompatibilityFinding(CompatibilityIssue.SelfReference, "Un documento no puede referenciarse a sí mismo"));
        if (note.Direction != original.Direction)
            reasons.Add(new CompatibilityFinding(CompatibilityIssue.Direction, "Dirección fiscal diferente"));
        if (string.IsNullOrWhiteSpace(note.IssuerIdentification) ||
            string.IsNullOrWhiteSpace(note.ReceiverIdentification) ||
            !string.Equals(note.IssuerIdentification, original.IssuerIdentification, StringComparison.Ordinal) ||
            !string.Equals(note.IssuerIdentificationType, original.IssuerIdentificationType, StringComparison.Ordinal))
            reasons.Add(new CompatibilityFinding(CompatibilityIssue.Issuer, "Identificación fiscal del emisor diferente"));
        if (string.IsNullOrWhiteSpace(note.ReceiverIdentification) ||
            !string.Equals(note.ReceiverIdentification, original.ReceiverIdentification, StringComparison.Ordinal) ||
            !string.Equals(note.ReceiverIdentificationType, original.ReceiverIdentificationType, StringComparison.Ordinal))
            reasons.Add(new CompatibilityFinding(CompatibilityIssue.Receiver, "Identificación fiscal del receptor diferente"));

        var number = reference.ReferenceNumber?.Trim() ?? string.Empty;
        if (number.Length == 0 ||
            (!string.Equals(number, original.Clave, StringComparison.Ordinal) &&
             !string.Equals(number, original.NumeroConsecutivo, StringComparison.Ordinal)))
            reasons.Add(new CompatibilityFinding(CompatibilityIssue.ReferenceNumber, "Número de referencia diferente"));

        if (!TryMapFiscalTypeCode(reference.ReferencedDocumentTypeCode, out var expectedType) ||
            original.DocumentType != expectedType)
            reasons.Add(new CompatibilityFinding(CompatibilityIssue.DocumentType, "Tipo de comprobante referenciado incompatible"));

        // DateTimeOffset.Date reads the issuer's calendar date without shifting to UTC.
        if (DateOnly.FromDateTime(reference.ReferencedIssueDate.Date) !=
            DateOnly.FromDateTime(original.FechaEmision.Date))
            reasons.Add(new CompatibilityFinding(CompatibilityIssue.IssueDate, "Fecha de emisión referenciada diferente"));

        if (string.IsNullOrWhiteSpace(note.CurrencyCode) ||
            !string.Equals(note.CurrencyCode.Trim(), original.CurrencyCode?.Trim(), StringComparison.OrdinalIgnoreCase))
            reasons.Add(new CompatibilityFinding(CompatibilityIssue.Currency, "Moneda diferente"));

        return reasons;
    }

    public static bool IsFiscalKey(string? number) =>
        number is not null && number.Length == 50 && number.All(char.IsAsciiDigit);

    private static bool TryMapFiscalTypeCode(string? code, out ElectronicDocumentType type)
    {
        type = code switch
        {
            "01" => ElectronicDocumentType.Invoice,
            "02" => ElectronicDocumentType.DebitNote,
            "03" => ElectronicDocumentType.CreditNote,
            "04" => ElectronicDocumentType.ElectronicTicket,
            "08" => ElectronicDocumentType.PurchaseInvoice,
            "09" => ElectronicDocumentType.ExportInvoice,
            "10" => ElectronicDocumentType.ElectronicPaymentReceipt,
            _ => default
        };
        return code is "01" or "02" or "03" or "04" or "08" or "09" or "10";
    }
}
