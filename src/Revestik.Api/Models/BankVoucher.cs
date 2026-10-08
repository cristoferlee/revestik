using Revestik.Shared.Expenses;

namespace Revestik.Api.Models;

public sealed class BankVoucher
{
    public int Id { get; set; }

    public string Bank { get; set; } = string.Empty;
    public string MerchantName { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTimeOffset TransactionDate { get; set; }

    public string? CardBrand { get; set; }
    public string? CardLastFour { get; set; }
    public string? AuthorizationNumber { get; set; }
    public string? ReferenceNumber { get; set; }

    public string GmailMessageId { get; set; } = string.Empty;

    public BankVoucherStatus Status { get; set; } =
        BankVoucherStatus.NeedsReview;

    public int? MatchedElectronicDocumentId { get; set; }
    public ElectronicDocument? MatchedElectronicDocument { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
