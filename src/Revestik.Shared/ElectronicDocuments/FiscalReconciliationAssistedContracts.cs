using System.ComponentModel.DataAnnotations;
namespace Revestik.Shared.ElectronicDocuments;
public sealed record FiscalWorklistItem(int Id, string DocumentType, string IssuerName, string Clave, DateTimeOffset IssueDate, string CurrencyCode, decimal Total, int? PurchaseId, string State);
public sealed record FiscalWorklistResponse(IReadOnlyList<FiscalWorklistItem> Items, int TotalCount, int Page, int PageSize);
public sealed record FiscalPurchaseCandidate(int PurchaseId, DateOnly Date, string SupplierName, string Currency, decimal LineCost, decimal NetDifference, int DaysApart, IReadOnlyList<string> Reasons);
public sealed record FiscalPurchaseCandidatesResponse(int DocumentId, string IssuerName, string Currency, decimal FiscalNet, decimal FiscalTotal, IReadOnlyList<FiscalPurchaseCandidate> Candidates);
public sealed record FiscalNoteReferenceItem(int ReferenceId, string Number, string Reason, int? LinkedDocumentId, int? SuggestedDocumentId, string MatchState);
public sealed record FiscalNoteReferencesResponse(int DocumentId, IReadOnlyList<FiscalNoteReferenceItem> References)
{
    public FiscalNoteAdjustmentPreview? Preview { get; init; }
    public IReadOnlyList<FiscalMatchedDocument> Matches { get; init; } = [];
    public IReadOnlyList<FiscalReferenceCandidateDiagnostic> Diagnostics { get; init; } = [];
}
public sealed record FiscalNoteAdjustmentPreview(int OriginalDocumentId, string CurrencyCode,
    decimal OriginalTotal, decimal NoteDelta, decimal ProjectedTotal, string Disclaimer)
{
    // Additive properties preserve existing JSON fields and positional constructor compatibility.
    public decimal CurrentAdjustedTotal { get; init; }
    public decimal ApprovedCreditNotesTotal { get; init; }
    public decimal ApprovedDebitNotesTotal { get; init; }
    public decimal PotentialExcess { get; init; }
}
public sealed record FiscalReferenceLinkRequest([property:Range(1,int.MaxValue)] int DocumentId);

public sealed record FiscalMatchedDocument(int Id, string Clave, string NumeroConsecutivo, string DocumentType,
    string IssuerName, string IssuerIdentification, DateTimeOffset IssueDate, string CurrencyCode, decimal TotalDocument);

public sealed record FiscalReferenceCandidateDiagnostic(int ReferenceId, FiscalMatchedDocument Document,
    bool Compatible, IReadOnlyList<string> Reasons);
