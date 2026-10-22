using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.Projects;
using Revestik.Shared.Projects;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectDashboardSummaryTests
{
    [Fact]
    public async Task Snapshot_SeparatesOpenClosedUnpricedAndVoidedPayments()
    {
        await using var db = new RevestikDbContext(
            new DbContextOptionsBuilder<RevestikDbContext>()
                .UseInMemoryDatabase($"dash04-{Guid.NewGuid():N}")
                .Options);

        db.Set<Project>().AddRange(
            new Project { Id = 1, CustomerId = 1, Name = "Open CRC", Status = ProjectStatus.Active,
                AgreedAmount = 100m, CurrencyCode = "CRC" },
            new Project { Id = 2, CustomerId = 1, Name = "Unpriced", Status = ProjectStatus.Planning },
            new Project { Id = 3, CustomerId = 1, Name = "Closed with balance", Status = ProjectStatus.Completed,
                AgreedAmount = 200m, CurrencyCode = "USD" },
            new Project { Id = 4, CustomerId = 1, Name = "Open USD", Status = ProjectStatus.Paused,
                AgreedAmount = 70m, CurrencyCode = "USD" });
        db.Set<ProjectPayment>().AddRange(
            new ProjectPayment { Id = 1, ProjectId = 1, Amount = 30m, CurrencyCode = "CRC", Status = "Active" },
            new ProjectPayment { Id = 2, ProjectId = 1, Amount = 40m, CurrencyCode = "CRC", Status = "Voided" },
            new ProjectPayment { Id = 3, ProjectId = 3, Amount = 50m, CurrencyCode = "USD", Status = "Active" },
            new ProjectPayment { Id = 4, ProjectId = 4, Amount = 70m, CurrencyCode = "USD", Status = "Active" });
        await db.SaveChangesAsync();

        var result = await new ProjectDashboardSummaryService(db).GetAsync(CancellationToken.None);

        Assert.Equal(4, result.TotalCount);
        Assert.Equal(3, result.OpenCount);
        Assert.Equal(1, result.CompletedCount);
        Assert.Equal(1, result.OpenProjectsWithoutAmount);
        Assert.Equal(1, result.CompletedProjectsWithBalance);
        var crc = Assert.Single(result.OpenCurrencies, x => x.CurrencyCode == "CRC");
        Assert.Equal(100m, crc.AgreedAmount);
        Assert.Equal(30m, crc.PaidAmount);
        Assert.Equal(70m, crc.RemainingAmount);
        var usd = Assert.Single(result.OpenCurrencies, x => x.CurrencyCode == "USD");
        Assert.Equal(70m, usd.AgreedAmount);
        Assert.Equal(70m, usd.PaidAmount);
        Assert.Equal(0m, usd.RemainingAmount);
    }
}
