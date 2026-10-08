namespace Revestik.Api.Services.BankVouchers;

public interface IBankVoucherEmailParser
{
    bool TryParse(
        string? subject,
        string body,
        out ParsedBankVoucher? voucher);
}
