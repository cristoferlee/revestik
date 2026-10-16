using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Projects;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Customers;
using Revestik.Shared.Projects;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectPaymentServiceSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string Owner = "b05bc-owner";
    private async Task<int> SeedAsync(decimal? agreed = 100m)
    {
        await using var db = fixture.CreateDbContext();
        if (!await db.Users.AnyAsync(x => x.Id == Owner))
            db.Users.Add(new ApplicationUser { Id = Owner, UserName = Owner,
                NormalizedUserName = Owner.ToUpperInvariant(), DisplayName = Owner,
                Email = "b05bc@example.com", NormalizedEmail = "B05BC@EXAMPLE.COM",
                SecurityStamp = Guid.NewGuid().ToString() });
        var customer = new Customer { Name = "B05 BC", IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9], Email = "b05bc@example.com",
            PhoneNumber = "88888888", ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
            OtherSigns = "Heredia", IsActive = true, CreatedAtUtc = DateTime.UtcNow };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var project = new Project { CustomerId = customer.Id, ResponsibleUserId = Owner,
            Name = suffix, NormalizedName = suffix.ToUpperInvariant(), AgreedAmount = agreed,
            CurrencyCode = agreed.HasValue ? "CRC" : null };
        db.Set<Project>().Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }
    private static ProjectPaymentUpsertRequest Payment(decimal amount, long? version = null, string? reason = null) =>
        new(amount, "CRC", new DateOnly(2026, 10, 16), "SINPE", "", version, reason);

    [Fact]
    public async Task CreateAndCorrection_PreserveHistoryAndBalance()
    {
        var id = await SeedAsync();
        int paymentId;
        await using (var db = fixture.CreateDbContext())
        {
            var created = await new ProjectPaymentService(db).CreateAsync(id, Payment(40), Owner, false, default);
            paymentId = created.Id;
            Assert.Equal(1, created.Version);
        }
        await using (var db = fixture.CreateDbContext())
        {
            var service = new ProjectPaymentService(db);
            var corrected = await service.CorrectAsync(id, paymentId, Payment(30, 1, "Ajuste de recibo"), Owner, false, default);
            Assert.Equal(2, corrected!.Version);
            Assert.Equal(70m, (await service.SummaryAsync(id, default))!.RemainingBalance);
            Assert.Single((await service.HistoryAsync(id, paymentId, default))!);
        }
    }

    [Fact]
    public async Task OverpaymentAndUnauthorizedMutation_AreRejected()
    {
        var id = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var service = new ProjectPaymentService(db);
        var denial = await Assert.ThrowsAsync<ProjectOperationException>(() => service.CreateAsync(id, Payment(1), "other", false, default));
        Assert.Equal(ProjectFailureReason.Forbidden, denial.Reason);
        await Assert.ThrowsAsync<ProjectOperationException>(() => service.CreateAsync(id, Payment(101), Owner, false, default));
        Assert.Empty(await db.Set<ProjectPayment>().Where(x => x.ProjectId == id).ToListAsync());
    }

    [Fact]
    public async Task Void_ExcludesPaymentWithoutDeletingIt()
    {
        var id = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var service = new ProjectPaymentService(db);
        var p = await service.CreateAsync(id, Payment(20), Owner, false, default);
        Assert.True(await service.VoidAsync(id, p.Id, new ProjectPaymentVoidRequest(1, "Pago reversado"), Owner, false, default));
        Assert.Equal(100m, (await service.SummaryAsync(id, default))!.RemainingBalance);
        Assert.Equal("Voided", (await service.GetAsync(id, p.Id, default))!.Status);
    }

    [Fact]
    public async Task ReduceAgreedAmount_RejectsAmountBelowPaid()
    {
        var id = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        var service = new ProjectPaymentService(db);
        await service.CreateAsync(id, Payment(80), Owner, false, default);
        await Assert.ThrowsAsync<ProjectOperationException>(() => service.ChangeAgreedAmountAsync(id,
            new ProjectAgreedAmountChangeRequest(50, "CRC", 1, "Reajuste"), Owner, false, default));
        Assert.Equal(100m, (await service.SummaryAsync(id, default))!.AgreedAmount);
    }
}
