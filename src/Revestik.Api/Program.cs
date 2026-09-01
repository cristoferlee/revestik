using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Endpoints;
using Revestik.Api.Extensions;
using Revestik.Api.Integrations.Hacienda;
using Revestik.Api.Integrations.Locations;
using Revestik.Api.Services.Customers;

const string ClientCorsPolicy = "ClientCorsPolicy";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

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

if (builder.Environment.IsDevelopment())
{
    var allowedOrigins = builder.Configuration
        .GetSection("AllowedOrigins")
        .Get<string[]>() ?? [];

    if (allowedOrigins.Length == 0)
    {
        throw new InvalidOperationException(
            "At least one development client origin must be configured.");
    }

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(ClientCorsPolicy, policy =>
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });
}

var connectionString = builder.Configuration
    .GetConnectionString("RevestikDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'RevestikDatabase' was not found.");

builder.Services.AddDbContext<RevestikDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

builder.Services.AddRevestikAuthentication(
    builder.Configuration);

builder.Services.AddScoped<ICustomerService, CustomerService>();

var haciendaBaseUrl =
    builder.Configuration["Hacienda:BaseUrl"]
    ?? throw new InvalidOperationException(
        "Hacienda base URL was not configured.");

builder.Services.AddHttpClient<
    IHaciendaTaxpayerClient,
    HaciendaTaxpayerClient>(httpClient =>
{
    httpClient.BaseAddress = new Uri(haciendaBaseUrl);
    httpClient.Timeout = TimeSpan.FromSeconds(10);
});

var locationCatalogBaseUrl =
    builder.Configuration["LocationCatalog:BaseUrl"]
    ?? throw new InvalidOperationException(
        "Location catalog base URL was not configured.");

builder.Services.AddMemoryCache();

builder.Services.AddHttpClient<
    ILocationCatalogClient,
    LocationCatalogClient>(httpClient =>
{
    httpClient.BaseAddress = new Uri(locationCatalogBaseUrl);
    httpClient.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseCors(ClientCorsPolicy);
}
else
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.Equals(
                "/index.html",
                StringComparison.OrdinalIgnoreCase))
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

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet(
        "/api/health",
        async (
            RevestikDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var canConnectToDatabase = await dbContext.Database
                .CanConnectAsync(cancellationToken);

            if (!canConnectToDatabase)
            {
                return Results.Problem(
                    title: "Database connection failed.",
                    statusCode:
                        StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(new
            {
                Status = "Healthy",
                Service = "Revestik.Api",
                Database = "Connected",
                TimestampUtc = DateTime.UtcNow
            });
        })
    .WithName("GetHealth")
    .WithTags("System")
    .AllowAnonymous();

app.MapCustomerEndpoints();
app.MapTaxpayerEndpoints();
app.MapLocationEndpoints();
app.MapAuthenticationEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.MapStaticAssets()
        .AllowAnonymous();

    app.MapFallback(
            "/api/{**path}",
            () => Results.NotFound())
        .AllowAnonymous();

    app.MapFallbackToFile(
            "index.html",
            new StaticFileOptions
            {
                OnPrepareResponse = static context =>
                    DisableClientIndexCaching(
                        context.Context.Response)
            })
        .AllowAnonymous();
}

await app.InitializeIdentityAsync();

app.Run();

static void DisableClientIndexCaching(
    HttpResponse response)
{
    response.Headers.CacheControl = "no-store, no-cache";
    response.Headers.Pragma = "no-cache";
    response.Headers.Expires = "0";
}

public partial class Program;
