namespace Revestik.Shared.Expenses;

public sealed record BankVoucherPageResponse(
    IReadOnlyList<BankVoucherReviewItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
