using Revestik.Api.Endpoints;
using Revestik.Api.Endpoints.PublicCatalog;
using Revestik.Api.Services.Expenses;

namespace Revestik.Api.Extensions;

public static class RevestikEndpointRegistrationExtensions
{
    public static IEndpointRouteBuilder MapRevestikApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCabysEndpoints();
        app.MapCustomerEndpoints();
        app.MapSupplierEndpoints();
        app.MapPurchaseEndpoints();
        app.MapExpenseEndpoints();
        app.MapGet("/api/dashboard/expense-summary", async (
            IExpenseReportingService reportingService,
            CancellationToken cancellationToken) =>
        {
            var costaRicaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Costa_Rica");
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, costaRicaTimeZone);
            var firstDay = new DateOnly(localNow.Year, localNow.Month, 1);
            var lastDay = firstDay.AddMonths(1).AddDays(-1);
            var request = new Revestik.Shared.Expenses.ExpenseSummaryRequest
            {
                DateFrom = firstDay,
                DateTo = lastDay
            };
            return Results.Ok(await reportingService.GetConsolidatedSummaryAsync(request, cancellationToken));
        })
        .WithName("GetDashboardMonthlyExpenseSummary")
        .WithTags("Dashboard")
        .RequireAuthorization();

        app.MapBankVoucherEndpoints();
        app.MapElectronicDocumentEndpoints();
        app.MapElectronicDocumentPeriodSummaryEndpoints();
        app.MapDashboardDocumentAttentionEndpoints();
        app.MapGrossMarginDashboardEndpoints();
        app.MapDashboardActivityTrendEndpoints();
        app.MapDashboardRecentActivityEndpoints();
        app.MapBccrExchangeRateEndpoints();
        app.MapOperatingResultDashboardEndpoints();
        app.MapGmailIntegrationEndpoints();
        app.MapBankVoucherGmailIntegrationEndpoints();
        app.MapPublicCatalogEndpoints();
        app.MapPublicCartEndpoints();
        app.MapPublicProformaEndpoints();
        app.MapPublicProjectReadEndpoints();
        app.MapPublicProjectAdminEndpoints();
        app.MapPublicProjectTagEndpoints();
        app.MapPublicHomeHeroEndpoints();
        app.MapPublicHomeSectionEndpoints();
        app.MapPublicSolutionsSectionEndpoints();
        app.MapProductEndpoints();
        app.MapInventoryCatalogEndpoints();
        app.MapInventoryEndpoints();
        app.MapInventoryCostLayerEndpoints();
        app.MapQuotationEndpoints();
        app.MapSaleEndpoints();
        app.MapTaxpayerEndpoints();
        app.MapLocationEndpoints();
        app.MapCompanyProfileEndpoints();
        app.MapCompanyLogoEndpoints();
        app.MapCompanyDocumentPdfPreferencesEndpoints();
        app.MapAuthenticationEndpoints();
        app.MapUserAdministrationEndpoints();
        app.MapSecurityAuditEndpoints();
        app.MapFiscalReconciliationEndpoints();
        app.MapProjectEndpoints();
        app.MapProjectActivityEndpoints();
        app.MapProjectEventDetailEndpoints();
        app.MapProjectMilestoneEndpoints();
        app.MapProjectDocumentEndpoints();
        app.MapProjectReportEndpoints();
        app.MapProjectPaymentEndpoints();
        app.MapProjectTaskDeadlineEndpoints();
        app.MapProjectLifecycleEndpoints();
        app.MapProjectDashboardAlertEndpoints();

        return app;
    }
}
