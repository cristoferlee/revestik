using Revestik.Api.Configuration;

namespace Revestik.Api.Tests.GmailIntegration;

public sealed class BankVoucherGmailIntegrationOptionsTests
{
    [Fact]
    public void Defaults_TargetBancoNacionalVoucherMailboxPattern()
    {
        var options = new BankVoucherGmailIntegrationOptions();

        Assert.Equal("revestikcr@gmail.com", options.ExpectedMailbox);
        Assert.Equal("bncontacto@bncr.fi.cr", options.ExpectedSender);
        Assert.Equal("Voucher Digital", options.ExpectedSubject);
        Assert.Equal(100, options.MaxMessagesPerSync);
    }
}
