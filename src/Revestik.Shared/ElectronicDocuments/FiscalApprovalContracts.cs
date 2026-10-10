namespace Revestik.Shared.ElectronicDocuments;

public sealed record FiscalApprovalRequest(int ReferenceId, int OriginalDocumentId);
public sealed record FiscalApprovedAdjustment(int NoteId, string NoteClave, string DocumentType, decimal SignedAmount);
public sealed record FiscalAdjustedTotal(int DocumentId, string CurrencyCode, decimal OriginalTotal,
    decimal CreditNotesTotal, decimal DebitNotesTotal, decimal AdjustedTotal,
    IReadOnlyList<FiscalApprovedAdjustment> Adjustments);
