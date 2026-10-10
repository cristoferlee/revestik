namespace Revestik.Shared.ElectronicDocuments;

// Amounts represent fiscal evidence, not operational expenses or tax credits.
public sealed record FiscalDocumentCounterCurrencyTotal(
    string CurrencyCode,
    decimal InvoiceTotal,
    decimal CreditNoteTotal,
    decimal DebitNoteTotal,
    decimal NetTotal);

public sealed record FiscalDocumentCounterItem(
    int DocumentId,
    string Clave,
    DateOnly IssueDate,
    string CurrencyCode,
    ElectronicDocumentType DocumentType,
    decimal OriginalTotal,
    decimal SignedTotal,
    bool IsIncluded,
    string? ExclusionReason);

public sealed record FiscalDocumentCounterResponse(
    IReadOnlyList<FiscalDocumentCounterCurrencyTotal> Totals,
    IReadOnlyList<FiscalDocumentCounterItem> Documents);
