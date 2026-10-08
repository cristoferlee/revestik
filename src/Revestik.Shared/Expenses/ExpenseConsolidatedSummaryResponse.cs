namespace Revestik.Shared.Expenses;

public sealed record ExpenseConsolidatedSummaryResponse(
    IReadOnlyList<ExpenseConsolidatedCurrencyTotalResponse> Totals,
    int NeedsReviewVoucherCount,
    int AcceptedVoucherCount,
    int MatchedVoucherCount,
    int IgnoredVoucherCount);
