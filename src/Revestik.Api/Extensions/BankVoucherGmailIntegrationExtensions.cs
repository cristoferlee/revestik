using Revestik.Api.Configuration;
using Revestik.Api.Services.BankVouchers;
using Revestik.Api.Services.GmailIntegration;

namespace Revestik.Api.Extensions;

public static class BankVoucherGmailIntegrationExtensions
{
    public static IServiceCollection AddBankVoucherGmailIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<BankVoucherGmailIntegrationOptions>()
            .Bind(configuration.GetSection(BankVoucherGmailIntegrationOptions.SectionName))
            .PostConfigure(options =>
            {
                options.ClientId ??= configuration["GmailIntegration:ClientId"];
                options.ClientSecret ??= configuration["GmailIntegration:ClientSecret"];
            });

        services.AddSingleton<GmailOAuthClient>();
        services.AddSingleton<IBankVoucherGmailIntegrationStateStore, BankVoucherGmailIntegrationStateStore>();
        services.AddSingleton<IBankVoucherGmailOAuthStateService, BankVoucherGmailOAuthStateService>();
        services.AddSingleton<BankVoucherGmailSyncCoordinator>();

        services.AddSingleton<IBankVoucherEmailParser, BancoNacionalBankVoucherEmailParser>();
        services.AddScoped<IBankVoucherIngestionService, BankVoucherIngestionService>();
        services.AddScoped<IBankVoucherGmailIntegrationService, BankVoucherGmailIntegrationService>();

        return services;
    }
}
