namespace Revestik.Api.Services.BankVouchers;

public sealed record ParsedBankVoucher(
    string Bank,
    string MerchantName,
    decimal Amount,
    string Currency,
    DateTimeOffset TransactionDate,
    string? CardBrand,
    string? CardLastFour,
    string? AuthorizationNumber,
    string? ReferenceNumber);
