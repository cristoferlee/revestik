using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration
    .GetConnectionString("RevestikDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'RevestikDatabase' was not found.");

builder.Services.AddDbContext<RevestikDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

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

app.Run();