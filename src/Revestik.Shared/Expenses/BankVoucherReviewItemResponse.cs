namespace Revestik.Shared.Expenses;

public sealed record BankVoucherReviewItemResponse(
    int Id,
    string Bank,
    string MerchantName,
    decimal Amount,
    string Currency,
    DateTimeOffset TransactionDate,
    string? CardBrand,
    string? CardLastFour,
    string? AuthorizationNumber,
    string? ReferenceNumber,
    BankVoucherStatus Status,
    int? MatchedElectronicDocumentId,
    bool IsKnownMerchant,
    IReadOnlyList<BankVoucherMatchCandidateResponse> MatchCandidates,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
