namespace Revestik.Shared.Expenses;

public sealed record OperatingResultCurrencyResponse(
    string Currency,
    decimal CommercialResult,
    decimal EligibleDocumentExpenses,
    decimal EstimatedOperatingResult,
    decimal ExcludedManualExpenses,
    decimal ExcludedAcceptedVouchers,
    int DocumentsRequiringReview,
    bool RequiresReview);

public sealed record OperatingResultSummaryResponse(
    int Year, int Month, IReadOnlyList<OperatingResultCurrencyResponse> Currencies);
