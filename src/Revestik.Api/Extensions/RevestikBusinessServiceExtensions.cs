using Revestik.Api.Services.BankVouchers;
using Revestik.Api.Services.Cabys;
using Revestik.Api.Services.Customers;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Api.Services.Expenses;
using Revestik.Api.Services.GmailIntegration;
using Revestik.Api.Services.HaciendaXml;
using Revestik.Api.Services.Inventory;
using Revestik.Api.Services.Products;
using Revestik.Api.Services.Projects;
using Revestik.Api.Services.Projects.Documents;
using Revestik.Api.Services.Projects.Pdf;
using Revestik.Api.Services.Purchases;
using Revestik.Api.Services.Quotations;
using Revestik.Api.Services.Quotations.Pdf;
using Revestik.Api.Services.Sales;
using Revestik.Api.Services.Sales.Pdf;
using Revestik.Api.Services.Suppliers;

namespace Revestik.Api.Extensions;

public static class RevestikBusinessServiceExtensions
{
    public static IServiceCollection AddRevestikBusinessServices(
        this IServiceCollection services)
    {
        services.AddScoped<ICabysCatalogService, CabysCatalogService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<IPurchaseQueryService, PurchaseQueryService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IExpenseReportingService, ExpenseReportingService>();
        services.AddScoped<IBankVoucherReviewService, BankVoucherReviewService>();
        services.AddScoped<IElectronicDocumentXmlParser, ElectronicDocumentXmlParser>();
        services.AddScoped<IElectronicDocumentImportService, ElectronicDocumentImportService>();
        services.AddScoped<IElectronicDocumentService, ElectronicDocumentService>();
        services.AddScoped<IReceivedDocumentInboxService, ReceivedDocumentInboxService>();
        services.AddSingleton<IHaciendaXmlSchemaValidator, HaciendaXmlSchemaValidator>();
        services.AddSingleton<IGmailIntegrationStateStore, GmailIntegrationStateStore>();
        services.AddSingleton<IGmailOAuthStateService, GmailOAuthStateService>();
        services.AddSingleton<GmailSyncCoordinator>();
        services.AddSingleton<GmailHistoricalSyncWorker>();
        services.AddHostedService(provider => provider.GetRequiredService<GmailHistoricalSyncWorker>());
        services.AddScoped<IGmailIntegrationService, GmailIntegrationService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<IInventoryPurchaseReceiptService, InventoryPurchaseReceiptService>();
        services.AddScoped<IInventoryService, PricingAwareInventoryService>();
        services.AddScoped<IInventoryCostResolutionService, InventoryCostResolutionService>();
        services.AddScoped<IInventoryPhysicalCountService, InventoryPhysicalCountService>();
        services.AddScoped<IInventoryCatalogService, InventoryCatalogService>();
        services.AddScoped<IQuotationService, QuotationService>();
        services.AddScoped<IQuotationNumberGenerator, SqlQuotationNumberGenerator>();
        services.AddSingleton<IQuotationPdfService, QuotationPdfService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<DashboardActivityTrendService>();
        services.AddScoped<ISaleNumberGenerator, SqlSaleNumberGenerator>();
        services.AddSingleton<ISalePdfService, SalePdfService>();
        services.AddScoped<FiscalAnalyticsService>();
        services.AddScoped<ElectronicDocumentPurchaseLinkService>();
        services.AddScoped<ElectronicDocumentPurchaseReconciliationService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ProjectActivityService>();
        services.AddScoped<ProjectEventDetailService>();
        services.AddScoped<ProjectMilestoneService>();
        services.AddScoped<ProjectMilestoneTemplateService>();
        services.AddScoped<ProjectDocumentService>();
        services.AddScoped<ProjectDocumentQueryService>();
        services.AddScoped<ProjectReportPdfService>();
        services.AddScoped<ProjectPaymentService>();
        services.AddScoped<ProjectTaskDeadlineService>();
        services.AddScoped<ProjectLifecycleService>();
        services.AddScoped<ProjectDeadlineQueryService>();
        services.AddScoped<ProjectDashboardSummaryService>();
        services.AddScoped<Revestik.Api.Services.Dashboard.DashboardRecentActivityService>();
        services.AddSingleton<Revestik.Api.Services.Dashboard.DashboardFinancialCache>();
        services.AddScoped<GrossMarginSummaryService>();
        services.AddScoped<OperatingResultSummaryService>();
        return services;
    }
}
