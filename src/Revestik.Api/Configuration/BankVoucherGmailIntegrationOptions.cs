namespace Revestik.Api.Configuration;

public sealed class BankVoucherGmailIntegrationOptions
{
    public const string SectionName = "BankVoucherGmailIntegration";

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string ExpectedMailbox { get; set; } = "revestikcr@gmail.com";
    public string ExpectedSender { get; set; } = "bncontacto@bncr.fi.cr";
    public string ExpectedSubject { get; set; } = "Voucher Digital";
    public string RedirectUri { get; set; } =
        "https://localhost:7126/api/integrations/gmail/bank-vouchers/oauth/callback";
    public string ClientBaseUrl { get; set; } = "https://localhost:7081";
    public string StateStoragePath { get; set; } = "App_Data/bank-voucher-gmail.state";
    public int InitialLookbackDays { get; set; } = 30;
    public int MaxMessagesPerSync { get; set; } = 100;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        !string.IsNullOrWhiteSpace(ExpectedMailbox) &&
        !string.IsNullOrWhiteSpace(ExpectedSender) &&
        !string.IsNullOrWhiteSpace(ExpectedSubject) &&
        Uri.TryCreate(RedirectUri, UriKind.Absolute, out _) &&
        Uri.TryCreate(ClientBaseUrl, UriKind.Absolute, out _);
}
