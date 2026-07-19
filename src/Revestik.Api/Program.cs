using Revestik.Api.Endpoints;
using Revestik.Api.Services.Customers;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Integrations.Hacienda;

const string ClientCorsPolicy = "ClientCorsPolicy";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(ClientCorsPolicy, policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var connectionString = builder.Configuration
    .GetConnectionString("RevestikDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'RevestikDatabase' was not found.");

builder.Services.AddDbContext<RevestikDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

builder.Services.AddScoped<ICustomerService, CustomerService>();

var haciendaBaseUrl = builder.Configuration["Hacienda:BaseUrl"]
    ?? throw new InvalidOperationException(
        "Hacienda base URL was not configured.");

builder.Services.AddHttpClient<
    IHaciendaTaxpayerClient,
    HaciendaTaxpayerClient>(httpClient =>
{
    httpClient.BaseAddress = new Uri(haciendaBaseUrl);
    httpClient.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(ClientCorsPolicy);

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
                statusCode: StatusCodes.Status503ServiceUnavailable);
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
    .WithTags("System");


app.MapCustomerEndpoints();

app.MapTaxpayerEndpoints();


app.Run();