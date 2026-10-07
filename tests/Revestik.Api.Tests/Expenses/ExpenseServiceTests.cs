using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Services.Expenses;
using Revestik.Shared.Expenses;

namespace Revestik.Api.Tests.Expenses;

public sealed class ExpenseServiceTests
{
    [Fact]
    public async Task CreateAsync_NormalizesAndPersistsExpense()
    {
        await using var db = CreateDb();
        var service = new ExpenseService(db);

        var result = await service.CreateAsync(
            new ExpenseCreateRequest
            {
                Name = "  Pago de planilla  ",
                Description = "  Segunda quincena de septiembre.  ",
                TotalAmount = 850000m,
                ExpenseDate = new DateOnly(2026, 9, 30)
            },
            CancellationToken.None);

        Assert.Equal("Pago de planilla", result.Name);
        Assert.Equal(
            "Segunda quincena de septiembre.",
            result.Description);
        Assert.Equal(850000m, result.TotalAmount);
        Assert.Equal(new DateOnly(2026, 9, 30), result.ExpenseDate);
        Assert.True(result.CreatedAtUtc > DateTime.UtcNow.AddMinutes(-1));
        Assert.Single(db.Expenses);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_RejectsBlankDescription(string description)
    {
        await using var db = CreateDb();
        var service = new ExpenseService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(
                new ExpenseCreateRequest
                {
                    Name = "Gasto",
                    Description = description,
                    TotalAmount = 1000m,
                    ExpenseDate = new DateOnly(2026, 10, 1)
                },
                CancellationToken.None));
    }

    [Fact]
    public async Task GetPageAsync_SearchesNameAndDescriptionAndOrdersByDate()
    {
        await using var db = CreateDb();
        var service = new ExpenseService(db);

        await SeedAsync(
            service,
            "Planilla",
            "Pago mensual",
            100m,
            new DateOnly(2026, 10, 1));
        await SeedAsync(
            service,
            "Mantenimiento",
            "Reparación de oficina",
            200m,
            new DateOnly(2026, 10, 3));
        await SeedAsync(
            service,
            "Otro",
            "Pago de Planilla extraordinario",
            300m,
            new DateOnly(2026, 10, 2));

        var result = await service.GetPageAsync(
            new ExpenseListRequest
            {
                Search = "Planilla",
                Page = 1,
                PageSize = 20
            },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal("Otro", result.Items[0].Name);
        Assert.Equal("Planilla", result.Items[1].Name);
    }

    [Fact]
    public async Task GetPageAsync_FiltersByDateRange()
    {
        await using var db = CreateDb();
        var service = new ExpenseService(db);

        await SeedAsync(
            service,
            "Septiembre",
            "Gasto anterior",
            100m,
            new DateOnly(2026, 9, 30));
        await SeedAsync(
            service,
            "Octubre",
            "Gasto del período",
            200m,
            new DateOnly(2026, 10, 2));

        var result = await service.GetPageAsync(
            new ExpenseListRequest
            {
                DateFrom = new DateOnly(2026, 10, 1),
                DateTo = new DateOnly(2026, 10, 31)
            },
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("Octubre", item.Name);
    }

    [Fact]
    public async Task GetSummaryAsync_UsesOnlyRequestedDateRange()
    {
        await using var db = CreateDb();
        var service = new ExpenseService(db);

        await SeedAsync(
            service,
            "Fuera",
            "Septiembre",
            100m,
            new DateOnly(2026, 9, 30));
        await SeedAsync(
            service,
            "Uno",
            "Octubre",
            200m,
            new DateOnly(2026, 10, 2));
        await SeedAsync(
            service,
            "Dos",
            "Octubre",
            300m,
            new DateOnly(2026, 10, 5));

        var result = await service.GetSummaryAsync(
            new ExpenseSummaryRequest
            {
                DateFrom = new DateOnly(2026, 10, 1),
                DateTo = new DateOnly(2026, 10, 31)
            },
            CancellationToken.None);

        Assert.Equal(2, result.ExpenseCount);
        Assert.Equal(500m, result.TotalAmount);
    }

    private static RevestikDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase($"ExpenseTests-{Guid.NewGuid()}")
            .Options);

    private static Task SeedAsync(
        ExpenseService service,
        string name,
        string description,
        decimal amount,
        DateOnly date) =>
        service.CreateAsync(
            new ExpenseCreateRequest
            {
                Name = name,
                Description = description,
                TotalAmount = amount,
                ExpenseDate = date
            },
            CancellationToken.None);
}
