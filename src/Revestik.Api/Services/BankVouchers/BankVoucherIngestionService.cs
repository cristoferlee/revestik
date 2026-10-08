using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Shared.Expenses;

namespace Revestik.Api.Services.BankVouchers;

public sealed class BankVoucherIngestionService(
    RevestikDbContext dbContext,
    IBankVoucherEmailParser parser)
    : IBankVoucherIngestionService
{
    public Task<bool> ExistsAsync(
        string gmailMessageId,
        CancellationToken cancellationToken)
    {
        var normalizedMessageId = NormalizeMessageId(gmailMessageId);

        return dbContext.BankVouchers
            .AsNoTracking()
            .AnyAsync(
                voucher => voucher.GmailMessageId == normalizedMessageId,
                cancellationToken);
    }

    public async Task<BankVoucherImportOutcome> ImportAsync(
        string gmailMessageId,
        string? subject,
        IReadOnlyList<string> bodies,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        var normalizedMessageId = NormalizeMessageId(gmailMessageId);

        if (await ExistsAsync(normalizedMessageId, cancellationToken))
            return BankVoucherImportOutcome.Duplicate;

        ParsedBankVoucher? parsed = null;

        foreach (var body in bodies)
        {
            if (parser.TryParse(subject, body, out parsed) && parsed is not null)
                break;
        }

        if (parsed is null)
            return BankVoucherImportOutcome.Unrecognized;

        var voucher = new BankVoucher
        {
            Bank = parsed.Bank,
            MerchantName = parsed.MerchantName,
            Amount = parsed.Amount,
            Currency = parsed.Currency,
            TransactionDate = parsed.TransactionDate,
            CardBrand = parsed.CardBrand,
            CardLastFour = parsed.CardLastFour,
            AuthorizationNumber = parsed.AuthorizationNumber,
            ReferenceNumber = parsed.ReferenceNumber,
            GmailMessageId = normalizedMessageId,
            Status = BankVoucherStatus.NeedsReview,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.BankVouchers.Add(voucher);
        await dbContext.SaveChangesAsync(cancellationToken);

        return BankVoucherImportOutcome.Imported;
    }

    private static string NormalizeMessageId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "The Gmail message id is required.",
                nameof(value));
        }

        return value.Trim();
    }
}
