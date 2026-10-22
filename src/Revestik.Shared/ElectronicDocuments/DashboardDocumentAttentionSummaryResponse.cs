namespace Revestik.Shared.ElectronicDocuments;

public sealed record DashboardDocumentAttentionSummaryResponse(
    int ReceivedCount,
    int PendingClassificationCount,
    int PendingFiscalReconciliationCount,
    int PendingInvoiceLinkCount,
    int PendingNoteReviewCount);
