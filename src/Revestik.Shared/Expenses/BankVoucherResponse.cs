namespace Revestik.Shared.Expenses;

public sealed record BankVoucherResponse(
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
    string GmailMessageId,
    BankVoucherStatus Status,
    int? MatchedElectronicDocumentId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
