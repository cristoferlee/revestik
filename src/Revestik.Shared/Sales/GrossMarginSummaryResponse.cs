namespace Revestik.Shared.Sales;

// Monetary values are always currency-specific and never converted implicitly.
public sealed record GrossMarginCurrencyResponse(
    Currency Currency,
    decimal VerifiedProductRevenue,
    decimal VerifiedFifoCost,
    decimal VerifiedGrossMargin,
    decimal? VerifiedMarginPercent,
    decimal UnverifiedProductRevenue,
    int UnverifiedProductGroups,
    decimal ServiceRevenue,
    decimal AdditionalCharges,
    int VerifiedProductGroups,
    decimal DirectPurchaseBase,
    decimal ManualLineRevenue,
    decimal AllSalesLineRevenue,
    decimal EstimatedCommercialResult,
    bool RequiresReview,
    int DocumentsRequiringReview);

public sealed record GrossMarginSummaryResponse(
    int Year, int Month, IReadOnlyList<GrossMarginCurrencyResponse> Currencies);
