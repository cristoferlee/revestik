namespace Revestik.Api.Configuration;

public sealed class GmailIntegrationOptions
{
    public const string SectionName = "GmailIntegration";

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string ExpectedMailbox { get; set; } = "facturas@revestikcr.com";
    public string RedirectUri { get; set; } =
        "https://localhost:7126/api/integrations/gmail/oauth/callback";
    public string ClientBaseUrl { get; set; } = "https://localhost:7081";
    public string StateStoragePath { get; set; } = "App_Data/gmail-integration.state";
    public int InitialLookbackDays { get; set; } = 30;
    public int MaxMessagesPerSync { get; set; } = 500;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        !string.IsNullOrWhiteSpace(ExpectedMailbox) &&
        Uri.TryCreate(RedirectUri, UriKind.Absolute, out _) &&
        Uri.TryCreate(ClientBaseUrl, UriKind.Absolute, out _);
}
