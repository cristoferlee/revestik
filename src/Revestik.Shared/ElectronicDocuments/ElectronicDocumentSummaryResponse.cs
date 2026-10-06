namespace Revestik.Shared.ElectronicDocuments;

public sealed record ElectronicDocumentAccountingTotalResponse(
    string CurrencyCode,
    decimal PurchasesTotal,
    decimal ExpensesTotal,
    decimal DirectCostsTotal,
    decimal AssetsTotal,
    decimal UnallocatedTotal);

public sealed record ElectronicDocumentFinancialTotalResponse(
    string CurrencyCode,
    decimal CashTotal,
    decimal CreditTotal,
    decimal OtherTotal);

public sealed record ElectronicDocumentSummaryResponse(
    int PendingCount,
    int ProcessedCount,
    int NoActionRequiredCount,
    int WithHaciendaResponseCount,
    IReadOnlyList<ElectronicDocumentAccountingTotalResponse>? AccountingTotals = null,
    IReadOnlyList<ElectronicDocumentFinancialTotalResponse>? FinancialTotals = null);
