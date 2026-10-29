using Revestik.Api.Services.Company;
using Revestik.Api.Integrations.Bccr;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using QuestPDF.Infrastructure;
using Revestik.Api.Configuration;
using Revestik.Api.Data;
using Revestik.Api.Endpoints;
using Revestik.Api.Extensions;
using Revestik.Api.Integrations.Hacienda;
using Revestik.Api.Integrations.Locations;
using Revestik.Api.Services.BankVouchers;
using Revestik.Api.Services.Cabys;
using Revestik.Api.Services.Customers;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Api.Services.Expenses;
using Revestik.Api.Services.HaciendaXml;
using Revestik.Api.Services.GmailIntegration;
using Revestik.Api.Services.Inventory;
using Revestik.Api.Services.Products;
using Revestik.Api.Services.Purchases;
using Revestik.Api.Services.Quotations;
using Revestik.Api.Services.Quotations.Pdf;
using Revestik.Api.Services.Sales;
using Revestik.Api.Services.Sales.Pdf;
using Revestik.Api.Services.Suppliers;
using Revestik.Api.Services.Projects;
using Revestik.Api.Services.Projects.Documents;
using Revestik.Api.Services.Projects.Pdf;
using Revestik.Api.Services.Users;

const string ClientCorsPolicy = "ClientCorsPolicy";
QuestPDF.Settings.License = LicenseType.Community;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddRevestikRateLimiting();

// Explicit proxy IPs remain the default. A separate, opt-in Azure Container Apps
// mode exists for dynamic ingress proxies, but MUST NOT be enabled until the
// deployment guarantees that untrusted clients cannot reach Kestrel directly.
if (builder.Environment.IsProduction())
{
    var trustDynamicIngress = string.Equals(
        builder.Configuration["ReverseProxy:TrustDynamicAzureIngress"],
        "true", StringComparison.OrdinalIgnoreCase);
    var proxyIpValues = builder.Configuration["ReverseProxy:TrustedProxyIps"];
    if (trustDynamicIngress && !string.IsNullOrWhiteSpace(proxyIpValues))
        throw new InvalidOperationException(
            "ReverseProxy dynamic ingress and explicit trusted proxy IPs are mutually exclusive.");
    if (trustDynamicIngress && string.IsNullOrWhiteSpace(builder.Configuration["CONTAINER_APP_NAME"]))
        throw new InvalidOperationException(
            "Dynamic ingress trust is only supported inside Azure Container Apps.");

    if (trustDynamicIngress || !string.IsNullOrWhiteSpace(proxyIpValues))
    {
        var trustedProxies = new List<IPAddress>();
        if (!trustDynamicIngress)
        {
            foreach (var proxyIp in proxyIpValues!.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (!IPAddress.TryParse(proxyIp, out var parsedIp))
                    throw new InvalidOperationException(
                        "ReverseProxy:TrustedProxyIps must contain valid, explicit IP addresses.");
                trustedProxies.Add(parsedIp);
            }
            if (trustedProxies.Count == 0)
                throw new InvalidOperationException("ReverseProxy:TrustedProxyIps must not be empty.");
        }
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
            RevestikForwardedHeaders.Configure(options, trustDynamicIngress, trustedProxies));
    }
}
builder.Services.Configure<CompanyOptions>(builder.Configuration.GetSection(CompanyOptions.SectionName));
builder.Services.Configure<ReceivedDocumentInboxOptions>(builder.Configuration.GetSection(ReceivedDocumentInboxOptions.SectionName));

// Container Apps: store state only on a durable, private mounted volume.
// Local development retains its existing file locations.
if (builder.Environment.IsProduction())
{
    var persistentRoot = builder.Configuration["RevestikStorage:RootPath"];
    if (string.IsNullOrWhiteSpace(persistentRoot) || !Path.IsPathFullyQualified(persistentRoot))
        throw new InvalidOperationException("RevestikStorage:RootPath must be an absolute durable volume path.");
    persistentRoot = Path.GetFullPath(persistentRoot);
    if (!Directory.Exists(persistentRoot))
        throw new InvalidOperationException("Configured persistent volume does not exist. Check the volume mount before startup.");

    builder.Services.PostConfigure<ReceivedDocumentInboxOptions>(options =>
        options.StoragePath = Path.Combine(persistentRoot, "received-documents-inbox"));
    builder.Services.PostConfigure<GmailIntegrationOptions>(options =>
        options.StateStoragePath = Path.Combine(persistentRoot, "gmail", "gmail-integration.state"));
    builder.Services.PostConfigure<BankVoucherGmailIntegrationOptions>(options =>
        options.StateStoragePath = Path.Combine(persistentRoot, "gmail", "bank-voucher-gmail.state"));

    var keyDirectory = Path.Combine(persistentRoot, "dataprotection-keys");
    Directory.CreateDirectory(keyDirectory);
    var certificateValue = builder.Configuration["DataProtection:CertificateBase64"];
    var certificatePassword = builder.Configuration["DataProtection:CertificatePassword"];
    if (string.IsNullOrWhiteSpace(certificateValue) || string.IsNullOrWhiteSpace(certificatePassword))
        throw new InvalidOperationException("A certificate is required to encrypt persistent Data Protection keys.");
    var certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12(
        Convert.FromBase64String(certificateValue), certificatePassword,
        System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.EphemeralKeySet);
    if (!certificate.HasPrivateKey)
        throw new InvalidOperationException("Data Protection certificate must contain a private key.");
    builder.Services.AddDataProtection()
        .SetApplicationName("Revestik")
        .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory))
        .ProtectKeysWithCertificate(certificate);
}

builder.Services.Configure<GmailIntegrationOptions>(builder.Configuration.GetSection(GmailIntegrationOptions.SectionName));

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Host-Revestik.Csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Path = "/";
    options.Cookie.IsEssential = true;
});

// Keep multipart buffering below the document upload contract's maximum request size.
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 21L * 1024 * 1024;
    options.ValueLengthLimit = 4096;
    options.ValueCountLimit = 20;
});

if (builder.Environment.IsDevelopment())
{
    var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
    if (allowedOrigins.Length == 0)
        throw new InvalidOperationException("At least one development client origin must be configured.");

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(ClientCorsPolicy, policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials());
    });
}

var connectionString = builder.Configuration.GetConnectionString("RevestikDatabase")
    ?? throw new InvalidOperationException("Connection string 'RevestikDatabase' was not found.");
builder.Services.AddDbContext<RevestikDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddRevestikAuthentication(builder.Configuration);
builder.Services.AddScoped<UserAdministrationService>();
builder.Services.AddBankVoucherGmailIntegration(builder.Configuration);

builder.Services.AddScoped<ICabysCatalogService, CabysCatalogService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<IPurchaseQueryService, PurchaseQueryService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IExpenseReportingService, ExpenseReportingService>();
builder.Services.AddScoped<IBankVoucherReviewService, BankVoucherReviewService>();
builder.Services.AddScoped<IElectronicDocumentXmlParser, ElectronicDocumentXmlParser>();
builder.Services.AddScoped<IElectronicDocumentImportService, ElectronicDocumentImportService>();
builder.Services.AddScoped<IElectronicDocumentService, ElectronicDocumentService>();
builder.Services.AddScoped<IReceivedDocumentInboxService, ReceivedDocumentInboxService>();
builder.Services.AddSingleton<IHaciendaXmlSchemaValidator, HaciendaXmlSchemaValidator>();
builder.Services.AddSingleton<IGmailIntegrationStateStore, GmailIntegrationStateStore>();
builder.Services.AddSingleton<IGmailOAuthStateService, GmailOAuthStateService>();
builder.Services.AddSingleton<GmailSyncCoordinator>();
builder.Services.AddSingleton<GmailHistoricalSyncWorker>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<GmailHistoricalSyncWorker>());
builder.Services.AddScoped<IGmailIntegrationService, GmailIntegrationService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<IInventoryPurchaseReceiptService, InventoryPurchaseReceiptService>();
builder.Services.AddScoped<IInventoryService, PricingAwareInventoryService>();
builder.Services.AddScoped<IInventoryCostResolutionService, InventoryCostResolutionService>();
builder.Services.AddScoped<IInventoryPhysicalCountService, InventoryPhysicalCountService>();
builder.Services.AddScoped<IInventoryCatalogService, InventoryCatalogService>();
builder.Services.AddScoped<IQuotationService, QuotationService>();
builder.Services.AddScoped<IQuotationNumberGenerator, SqlQuotationNumberGenerator>();
builder.Services.AddSingleton<IQuotationPdfService, QuotationPdfService>();
builder.Services.AddScoped<ISaleService, SaleService>();
builder.Services.AddScoped<DashboardActivityTrendService>();
builder.Services.AddScoped<ISaleNumberGenerator, SqlSaleNumberGenerator>();
builder.Services.AddSingleton<ISalePdfService, SalePdfService>();
builder.Services.AddScoped<FiscalAnalyticsService>();
builder.Services.AddScoped<ElectronicDocumentPurchaseLinkService>();
builder.Services.AddScoped<ElectronicDocumentPurchaseReconciliationService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ProjectActivityService>();
builder.Services.AddScoped<ProjectEventDetailService>();
builder.Services.AddScoped<ProjectMilestoneService>();
builder.Services.AddScoped<ProjectMilestoneTemplateService>();
builder.Services.AddScoped<ProjectDocumentService>();
builder.Services.AddScoped<ProjectDocumentQueryService>();
builder.Services.AddScoped<ProjectReportPdfService>();
builder.Services.AddScoped<ProjectPaymentService>();
builder.Services.AddScoped<ProjectTaskDeadlineService>();
builder.Services.AddScoped<ProjectLifecycleService>();
builder.Services.AddScoped<ProjectDeadlineQueryService>();
builder.Services.AddScoped<ProjectDashboardSummaryService>();
builder.Services.AddScoped<Revestik.Api.Services.Dashboard.DashboardRecentActivityService>();
builder.Services.AddScoped<GrossMarginSummaryService>();
builder.Services.AddScoped<OperatingResultSummaryService>();
builder.Services.Configure<ProjectFileStorageOptions>(builder.Configuration.GetSection(ProjectFileStorageOptions.SectionName));
builder.Services.PostConfigure<ProjectFileStorageOptions>(options =>
{
    if (builder.Environment.IsProduction())
    {
        options.RootPath = Path.Combine(builder.Configuration["RevestikStorage:RootPath"]!, "project-documents");
        return;
    }
    if (string.IsNullOrWhiteSpace(options.RootPath))
        options.RootPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "RevestikPrivateDocuments"));
});
builder.Services.AddSingleton<IProjectFileStorage, LocalProjectFileStorage>();
builder.Services.AddSingleton<CompanyLogoStorage>();
builder.Services.AddScoped<CompanyPdfBrandingProvider>();

var haciendaBaseUrl = builder.Configuration["Hacienda:BaseUrl"]
    ?? throw new InvalidOperationException("Hacienda base URL was not configured.");
builder.Services.AddHttpClient<IHaciendaTaxpayerClient, HaciendaTaxpayerClient>(httpClient =>
{
    httpClient.BaseAddress = new Uri(haciendaBaseUrl);
    httpClient.Timeout = TimeSpan.FromSeconds(10);
});

var locationCatalogBaseUrl = builder.Configuration["LocationCatalog:BaseUrl"]
    ?? throw new InvalidOperationException("Location catalog base URL was not configured.");
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<BccrExchangeRateService>(httpClient =>
{
    httpClient.BaseAddress = new Uri("https://apim.bccr.fi.cr/SDDE/api/Bccr.Ge.SDDE.Publico.Indicadores.API/");
    httpClient.Timeout = TimeSpan.FromSeconds(12);
});
builder.Services.AddHttpClient<ILocationCatalogClient, LocationCatalogClient>(httpClient =>
{
    httpClient.BaseAddress = new Uri(locationCatalogBaseUrl);
    httpClient.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();
// Must run before HSTS, HTTPS redirection, routing and authentication.
// No forwarded headers are accepted unless a proxy mode is explicitly configured.
if (app.Environment.IsProduction() &&
    (string.Equals(app.Configuration["ReverseProxy:TrustDynamicAzureIngress"], "true", StringComparison.OrdinalIgnoreCase) ||
     !string.IsNullOrWhiteSpace(app.Configuration["ReverseProxy:TrustedProxyIps"])))
    app.UseForwardedHeaders();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
else
{
    // Suppress exception details in public responses. Detailed errors stay in server logs.
    app.UseExceptionHandler();
    app.UseHsts();
    app.Use(async (context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            return Task.CompletedTask;
        });
        await next(context);
    });
}
app.UseHttpsRedirection();
if (app.Environment.IsDevelopment()) app.UseCors(ClientCorsPolicy);
else
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.Equals("/index.html", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.OnStarting(() =>
            {
                DisableClientIndexCaching(context.Response);
                return Task.CompletedTask;
            });
        }
        await next(context);
    });
}

app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseAntiforgery();
app.MapGet("/api/health", async (RevestikDbContext dbContext, CancellationToken cancellationToken) =>
{
    var canConnectToDatabase = await dbContext.Database.CanConnectAsync(cancellationToken);
    if (!canConnectToDatabase)
        return Results.Problem(title: "Database connection failed.", statusCode: StatusCodes.Status503ServiceUnavailable);
    return Results.Ok(new { Status = "Healthy", Service = "Revestik.Api", Database = "Connected", TimestampUtc = DateTime.UtcNow });
}).WithName("GetHealth").WithTags("System").AllowAnonymous()
  .RequireRateLimiting(RevestikRateLimiting.Health);

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

if (!app.Environment.IsDevelopment())
{
    app.MapStaticAssets().AllowAnonymous();
    app.MapFallback("/api/{**path}", () => Results.NotFound()).AllowAnonymous();
    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        OnPrepareResponse = static context => DisableClientIndexCaching(context.Context.Response)
    }).AllowAnonymous();
}

await app.InitializeIdentityAsync();
app.Run();

static void DisableClientIndexCaching(HttpResponse response)
{
    response.Headers.CacheControl = "no-store, no-cache";
    response.Headers.Pragma = "no-cache";
    response.Headers.Expires = "0";
}

public partial class Program;
