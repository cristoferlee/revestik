namespace Revestik.Api.Services.BankVouchers;

public interface IBankVoucherIngestionService
{
    Task<bool> ExistsAsync(
        string gmailMessageId,
        CancellationToken cancellationToken);

    Task<BankVoucherImportOutcome> ImportAsync(
        string gmailMessageId,
        string? subject,
        IReadOnlyList<string> bodies,
        CancellationToken cancellationToken);
}
