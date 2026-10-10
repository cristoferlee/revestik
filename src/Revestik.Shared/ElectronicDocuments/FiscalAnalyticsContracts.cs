namespace Revestik.Shared.ElectronicDocuments;

public sealed record FiscalAnalyticsRow(
    string CurrencyCode, ElectronicDocumentType DocumentType,
    int DocumentCount, int LinkedPurchaseCount, int PendingReviewCount,
    decimal EvidenceTotal);

public sealed record FiscalAnalyticsResponse(
    DateOnly DateFrom, DateOnly DateTo,
    IReadOnlyList<FiscalAnalyticsRow> Rows);

public sealed record PurchaseLinkRequest(int PurchaseId);
